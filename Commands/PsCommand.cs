using System;
using System.Diagnostics;
using Microsoft.Diagnostics.NETCore.Client;

namespace DotAi.Commands;

public static class PsCommand
{
    // List diagnosable .NET processes on the machine.
    public static void Invoke(bool json = false, bool details = false)
    {
        try
        {
            var published = DiagnosticsClient.GetPublishedProcesses();
            if (published == null || !published.Any())
            {
                Console.WriteLine("No diagnosable processes found.");
                return;
            }

            if (json)
            {
                var list = new System.Collections.Generic.List<object>();
                foreach (var pid in published)
                {
                    string name = "<unknown>";
                    DateTime? start = null;
                    string? path = null;
                    try
                    {
                        var p = Process.GetProcessById(pid);
                        name = p.ProcessName;
                        start = p.StartTime;
                        try { path = p.MainModule?.FileName; } catch { path = null; }
                    }
                    catch {}

                    if (details)
                    {
                        list.Add(new { pid, name, start, path, diagnosable = true });
                    }
                    else
                    {
                        list.Add(new { pid, name });
                    }
                }

                var jsonText = Newtonsoft.Json.JsonConvert.SerializeObject(list, Newtonsoft.Json.Formatting.Indented);
                Console.WriteLine(jsonText);
                return;
            }

            Console.WriteLine("PID\tProcessName");
            if (!details)
            {
                foreach (var pid in published)
                {
                    string name = "<unknown>";
                    try { name = Process.GetProcessById(pid).ProcessName; } catch {}
                    Console.WriteLine($"{pid}\t{name}");
                }
                return;
            }

            // details table
            Console.WriteLine("PID\tProcessName\tStartTime\tPath");
            foreach (var pid in published)
            {
                string name = "<unknown>";
                string start = "";
                string path = "";
                try
                {
                    var p = Process.GetProcessById(pid);
                    name = p.ProcessName;
                    start = p.StartTime.ToString("O");
                    try { path = p.MainModule?.FileName ?? ""; } catch { path = "<no-access>"; }
                }
                catch {}

                Console.WriteLine($"{pid}\t{name}\t{start}\t{path}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to list processes: {ex.Message}");
        }
    }
}
