using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DotAi.Utilities;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.IO.Compression;

namespace DotAi.Commands;

// Minimal DAP client wrapper to drive a VS Code-compatible debug adapter (eg. vsdbg/netcoredbg)
// This is a pragmatic, CLI-focused helper that records adapter traffic and creates simple
// pause snapshots under sessions/<id>/debug when the adapter signals it stopped.
internal static class DebugCommand
{
    private class DapClient : IDisposable
    {
        private readonly Process _proc;
        private readonly Stream _inStream;
        private readonly Stream _outStream;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _readerTask;
        private int _seq = 1;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JObject>> _pending = new();
        private readonly StringBuilder _log = new();
        private readonly string _sessionId;
        private readonly string _debugDir;

        public DapClient(string sessionId, string adapterPath, string adapterArgs)
        {
            _sessionId = sessionId;
            _debugDir = Path.Combine(SessionHelper.GetSessionDir(sessionId), "debug");
            Directory.CreateDirectory(_debugDir);

            var psi = new ProcessStartInfo(adapterPath)
            {
                Arguments = adapterArgs,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start debug adapter");
            _inStream = _proc.StandardInput.BaseStream;
            _outStream = _proc.StandardOutput.BaseStream;

            // start reader loop
            _readerTask = Task.Run(() => ReaderLoopAsync(_cts.Token));

            // capture stderr into log as well
            _proc.ErrorDataReceived += (s, e) => { if (e.Data != null) AppendLog("ERR: " + e.Data + "\n"); };
            try { _proc.BeginErrorReadLine(); } catch { }
        }

        private void AppendLog(string s) { lock (_log) { _log.Append(s); } }

        private async Task ReaderLoopAsync(CancellationToken ct)
        {
            var r = new BinaryReader(_outStream, Encoding.UTF8);
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    // read header lines until blank line
                    string? header = await ReadHeaderAsync(r, ct).ConfigureAwait(false);
                    if (header == null) break;
                    // header contains Content-Length
                    var parts = header.Split(new[] { '\r','\n' }, StringSplitOptions.RemoveEmptyEntries);
                    int contentLength = 0;
                    foreach (var p in parts)
                    {
                        var idx = p.IndexOf(':');
                        if (idx > 0)
                        {
                            var k = p.Substring(0, idx).Trim();
                            var v = p.Substring(idx + 1).Trim();
                            if (string.Equals(k, "Content-Length", StringComparison.OrdinalIgnoreCase)) int.TryParse(v, out contentLength);
                        }
                    }
                    if (contentLength <= 0) continue;
                    var buf = r.ReadBytes(contentLength);
                    var payload = Encoding.UTF8.GetString(buf);
                    AppendLog("RECV: " + payload + "\n");
                    var j = JObject.Parse(payload);
                    // dispatch responses by id
                    if (j["id"] != null && j["id"].Type == JTokenType.Integer)
                    {
                        var id = j.Value<int>("id");
                        if (_pending.TryRemove(id, out var tcs)) tcs.TrySetResult(j);
                    }
                    // events: write to artifacts and handle stopped events
                    if (j["event"] != null)
                    {
                        var evt = j.Value<string>("event");
                        var body = j["body"] as JObject;
                        if (!string.IsNullOrEmpty(evt))
                        {
                            AppendLog($"EVENT: {evt}\n");
                            if (evt == "stopped")
                            {
                                _ = Task.Run(() => HandleStoppedAsync(body));
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { AppendLog("ReaderLoop error: " + ex.Message + "\n"); break; }
            }
        }

        private static async Task<string?> ReadHeaderAsync(BinaryReader r, CancellationToken ct)
        {
            var sb = new StringBuilder();
            string line;
            // read lines until empty line; BinaryReader has no ReadLine so we read bytes
            while (true)
            {
                var b = new List<byte>();
                int prev = -1;
                while (true)
                {
                    int v;
                    try { v = r.Read(); } catch { return null; }
                    if (v == -1) return null;
                    b.Add((byte)v);
                    int c = (byte)v;
                    if (prev == '\r' && c == '\n') break;
                    prev = c;
                }
                var s = Encoding.UTF8.GetString(b.ToArray());
                if (string.IsNullOrWhiteSpace(s)) return null;
                sb.Append(s);
                // header ends with double CRLF - check if we've read two CRLF sequences
                var str = sb.ToString();
                if (str.Contains("\r\n\r\n")) return str;
            }
        }

        public Task<JObject> SendRequestAsync(string command, object? parameters = null)
        {
            var id = Interlocked.Increment(ref _seq);
            var req = new JObject
            {
                ["seq"] = id,
                ["type"] = "request",
                ["command"] = command,
                ["arguments"] = parameters == null ? null : JObject.FromObject(parameters)
            };
            var txt = JsonConvert.SerializeObject(req);
            var bytes = Encoding.UTF8.GetBytes(txt);
            var header = Encoding.UTF8.GetBytes($"Content-Length: {bytes.Length}\r\n\r\n");
            AppendLog("SEND: " + txt + "\n");
            var tcs = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending[id] = tcs;
            lock (_inStream)
            {
                _inStream.Write(header, 0, header.Length);
                _inStream.Write(bytes, 0, bytes.Length);
                _inStream.Flush();
            }
            return tcs.Task;
        }

        private async Task HandleStoppedAsync(JObject? body)
        {
            try
            {
                // record the stopped event payload
                var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                var stoppedPath = Path.Combine(_debugDir, $"stopped-{stamp}.json");
                File.WriteAllText(stoppedPath, JsonConvert.SerializeObject(body, Formatting.Indented));
                SessionHelper.AddArtifact(_sessionId, "debug_stopped", stoppedPath);

                // try to get threads
                var threadsResp = await SendRequestAsync("threads");
                File.WriteAllText(Path.Combine(_debugDir, $"threads-{stamp}.json"), threadsResp.ToString(Formatting.Indented));
                SessionHelper.AddArtifact(_sessionId, "debug_threads", Path.Combine(_debugDir, $"threads-{stamp}.json"));

                // for each thread produce stacktrace and variables snapshot (best-effort)
                var bodyThreads = threadsResp["body"]?[
                    "threads"] as JArray;
                if (bodyThreads != null)
                {
                    foreach (var t in bodyThreads)
                    {
                        try
                        {
                            var tid = t.Value<int>("id");
                            var st = await SendRequestAsync("stackTrace", new { threadId = tid, startFrame = 0, levels = 50 });
                            var stPath = Path.Combine(_debugDir, $"stack-{stamp}-t{tid}.json");
                            File.WriteAllText(stPath, st.ToString(Formatting.Indented));
                            SessionHelper.AddArtifact(_sessionId, $"debug_stack_t{tid}", stPath);

                            // for top frame, get scopes and variables
                            var frames = st["body"]?[
                                "stackFrames"] as JArray;
                            if (frames != null && frames.Count > 0)
                            {
                                var top = frames[0];
                                var frameId = top.Value<int?>("id");
                                if (frameId != null)
                                {
                                    var scopes = await SendRequestAsync("scopes", new { frameId = frameId.Value });
                                    var scopesPath = Path.Combine(_debugDir, $"scopes-{stamp}-t{tid}.json");
                                    File.WriteAllText(scopesPath, scopes.ToString(Formatting.Indented));
                                    SessionHelper.AddArtifact(_sessionId, $"debug_scopes_t{tid}", scopesPath);

                                    var scopeArr = scopes["body"]?["scopes"] as JArray;
                                    if (scopeArr != null)
                                    {
                                        foreach (var sc in scopeArr)
                                        {
                                            try
                                            {
                                                var varRef = sc.Value<int?>("variablesReference");
                                                if (varRef != null && varRef.Value != 0)
                                                {
                                                    var vars = await SendRequestAsync("variables", new { variablesReference = varRef.Value });
                                                    var varsPath = Path.Combine(_debugDir, $"vars-{stamp}-t{tid}-r{varRef.Value}.json");
                                                    File.WriteAllText(varsPath, vars.ToString(Formatting.Indented));
                                                    SessionHelper.AddArtifact(_sessionId, $"debug_vars_t{tid}_r{varRef.Value}", varsPath);
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex) { AppendLog("HandleStopped error: " + ex.Message + "\n"); }
            finally
            {
                // persist current adapter log
                try { File.WriteAllText(Path.Combine(_debugDir, "adapter.log"), _log.ToString()); SessionHelper.AddArtifact(_sessionId, "debug_adapter_log", Path.Combine(_debugDir, "adapter.log")); } catch { }
            }
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }
            try { _readerTask.Wait(500); } catch { }
            try { _proc.Kill(); } catch { }
            try { File.WriteAllText(Path.Combine(_debugDir, "adapter.log"), _log.ToString()); SessionHelper.AddArtifact(_sessionId, "debug_adapter_log", Path.Combine(_debugDir, "adapter.log")); } catch { }
        }
    }

    // Public entry: attach to a process using an existing DAP-compatible adapter binary (eg. netcoredbg or vsdbg)
    public static void InvokeAttach(string sessionId, string adapterPath, int pid)
    {
        Console.WriteLine($"Launching debug adapter: {adapterPath} to attach to PID {pid}");
        try
        {
            // adapterArgs should put adapter into server mode that speaks DAP over stdio; many adapters accept --interpreter=vscode or --stdio
            var adapterArgs = "--interpreter=vscode";
            using var client = new DapClient(sessionId, adapterPath, adapterArgs);

            // initialize
            var init = client.SendRequestAsync("initialize", new { clientID = "dotai", adapterID = "dotnet", pathFormat = "path" }).GetAwaiter().GetResult();

            // attach
            var attachResp = client.SendRequestAsync("attach", new { processId = pid }).GetAwaiter().GetResult();

            // configurationDone
            var conf = client.SendRequestAsync("configurationDone").GetAwaiter().GetResult();

            Console.WriteLine("Attach sequence sent; adapter will send events. Press Enter to detach and exit.");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Debug attach failed: " + ex.Message);
        }
    }

    // Public: set a breakpoint (file path is host path).
    public static void InvokeSetBreakpoint(string sessionId, string adapterPath, string file, int line)
    {
        try
        {
            var adapterArgs = "--interpreter=vscode";
            using var client = new DapClient(sessionId, adapterPath, adapterArgs);
            var init = client.SendRequestAsync("initialize", new { clientID = "dotai", adapterID = "dotnet", pathFormat = "path" }).GetAwaiter().GetResult();
            var resp = client.SendRequestAsync("setBreakpoints", new { source = new { path = file }, breakpoints = new[] { new { line = line } } }).GetAwaiter().GetResult();
            Console.WriteLine("SetBreakpoints response: " + resp.ToString(Formatting.None));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("SetBreakpoint failed: " + ex.Message);
        }
    }

    // Download and install netcoredbg into tools/netcoredbg and return when complete.
    public static async Task InstallNetCoreDbg()
    {
        try
        {
            Console.WriteLine("Installing netcoredbg (latest release) into tools/netcoredbg...");
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("dotai-installer/1.0");
            var apiUrl = "https://api.github.com/repos/Samsung/netcoredbg/releases/latest";
            var resp = await client.GetStringAsync(apiUrl);
            var j = JObject.Parse(resp);
            var assets = j["assets"] as JArray;
            if (assets == null)
            {
                Console.WriteLine("No release assets found on GitHub for netcoredbg.");
                return;
            }

            string runtimeKey;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) runtimeKey = "win-x64";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) runtimeKey = "linux-x64";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) runtimeKey = "osx-x64";
            else runtimeKey = "";

            JToken? chosen = null;
            foreach (var a in assets)
            {
                var name = a.Value<string>("name") ?? string.Empty;
                if (!string.IsNullOrEmpty(runtimeKey) && name.IndexOf(runtimeKey, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    chosen = a; break;
                }
            }
            if (chosen == null) chosen = assets.First;
            var url = chosen.Value<string>("browser_download_url");
            var name2 = chosen.Value<string>("name");
            Console.WriteLine($"Downloading {name2} from {url} ...");

            var toolsDir = Path.Combine(Directory.GetCurrentDirectory(), "tools");
            Directory.CreateDirectory(toolsDir);
            var outPath = Path.Combine(toolsDir, name2);
            using (var s = await client.GetStreamAsync(url)) using (var fs = File.Create(outPath)) await s.CopyToAsync(fs);

            var extractDir = Path.Combine(toolsDir, "netcoredbg");
            Directory.CreateDirectory(extractDir);
            if (name2.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                ZipFile.ExtractToDirectory(outPath, extractDir, true);
            }
            else if (name2.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) || name2.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
            {
                // Try to call tar if present (platform dependent)
                try
                {
                    var psi = new ProcessStartInfo("tar") { Arguments = $"-xzf \"{outPath}\" -C \"{extractDir}\"", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                    var p = Process.Start(psi);
                    p.WaitForExit(60000);
                }
                catch
                {
                    Console.WriteLine("Downloaded tar.gz but automatic extraction failed. Please extract manually: " + outPath);
                }
            }

            // Try to find adapter binary
            string adapterExe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "netcoredbg.exe" : "netcoredbg";
            var found = Directory.GetFiles(extractDir, adapterExe, SearchOption.AllDirectories).FirstOrDefault();
            if (found != null)
            {
                Console.WriteLine($"netcoredbg installed at: {found}");
                Console.WriteLine("You can use it with: dotai debug attach --adapter <path-to-netcoredbg> --pid <pid>");
            }
            else
            {
                Console.WriteLine("netcoredbg downloaded but executable not found automatically. Extracted files are in: " + extractDir);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("InstallNetCoreDbg failed: " + ex.Message);
        }
    }
}
