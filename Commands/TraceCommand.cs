using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System.Diagnostics.Tracing;
using DotAi.Utilities;
using DotAi.Models;
using Newtonsoft.Json;

namespace DotAi.Commands;

public static class TraceCommand
{
    // Attach to a running dotnet process and record an EventPipe trace for `duration` seconds.
    // Writes a .nettrace-like payload (actual EventPipe stream) to output path.
    public static void Invoke(string? sessionId, int duration, string? output, int? pid = null)
    {
        var sid = sessionId ?? SessionHelper.CreateSession(pid, null, 30);
        var sessDir = SessionHelper.GetSessionDir(sid);
        Directory.CreateDirectory(sessDir);
        var outPath = output ?? Path.Combine(sessDir, "trace.nettrace");

        // Ensure metadata exists for the session so postprocess can find artifacts.
        var metaPath = Path.Combine(sessDir, "metadata.json");
        if (!File.Exists(metaPath))
        {
            try
            {
                var meta = new SessionMetadata
                {
                    SessionId = sid,
                    Pid = pid,
                    CreatedAt = DateTime.UtcNow
                };
                File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta, Formatting.Indented));
            }
            catch { }
        }

        if (pid is null)
        {
            Console.WriteLine("No --pid provided, creating an empty stub trace (use --pid to record real trace)");
            File.WriteAllText(outPath, $"Stub .nettrace for session {sid}\nDuration: {duration}s\nGenerated: {DateTime.UtcNow:O}\n");
            SessionHelper.AddArtifact(sid, "trace", outPath);
            Console.WriteLine($"Recorded (stub) trace -> {outPath}");
            return;
        }

        try
        {
            Console.WriteLine($"Attaching to PID {pid} and recording for {duration}s...");

            // Quick validation: check the diagnostics pipe list to see if this PID is a published .NET process
            var published = DiagnosticsClient.GetPublishedProcesses();
            if (!published.Contains(pid.Value))
            {
                Console.Error.WriteLine($"PID {pid.Value} does not appear in the list of .NET processes exposed to diagnostics.");
                Console.Error.WriteLine("Common causes: the target is not a .NET runtime, is running as a different user, or diagnostics are disabled.");
                Console.Error.WriteLine("Hints:");
                Console.Error.WriteLine(" - Ensure the target process is a .NET 6+/Core runtime.");
                Console.Error.WriteLine(" - Run as an account with permission to access the process (or run dotai as Administrator).");
                Console.Error.WriteLine(" - For additional info try: dotnet-trace ps (or `dotnet-trace` tool) to list diagnosable processes.");
                return;
            }

            var client = new DiagnosticsClient(pid.Value);

            // Basic providers for CPU stacks and GC/ASP.NET events
            var providers = new[] {
                // EventPipe expects System.Diagnostics.Tracing.EventLevel for level
                // Enable CPU/JIT and SampleProfiler keywords; include Default keywords to capture typical runtime events
                new EventPipeProvider("Microsoft-Windows-DotNETRuntime", System.Diagnostics.Tracing.EventLevel.Informational, (long)(ClrTraceEventParser.Keywords.Default | ClrTraceEventParser.Keywords.Jit)),
                // Add an explicit sample profiler provider in case the runtime exposes it under a different name
                new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", System.Diagnostics.Tracing.EventLevel.Verbose)
            };

            using var session = client.StartEventPipeSession(providers, false);
            using var fs = File.Create(outPath);

            // Pipe the EventPipe stream to disk
            var t = Task.Run(() => session.EventStream.CopyTo(fs));
            Task.Delay(TimeSpan.FromSeconds(duration)).Wait();
            session.Stop();
            t.Wait();

            SessionHelper.AddArtifact(sid, "trace", outPath);
            Console.WriteLine($"Recorded trace -> {outPath}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Trace failed: {ex.Message}");
        }
    }
}
