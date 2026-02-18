using System;
using System.Diagnostics;
using Microsoft.Diagnostics.NETCore.Client;

namespace DotAi.Utilities;

internal static class Interactive
{
    // Present a numbered list of diagnosable .NET processes and ask the user to pick one.
    // Returns chosen PID or null if cancelled or invalid.
    public static int? ChoosePidInteractive()
    {
        try
        {
            var published = DiagnosticsClient.GetPublishedProcesses();
            if (published == null)
            {
                Console.WriteLine("No diagnosable .NET processes found.");
                return null;
            }

            var list = new System.Collections.Generic.List<(int pid, string name)>();
            foreach (var pid in published)
            {
                string name = "<unknown>";
                try { name = Process.GetProcessById(pid).ProcessName; } catch { }
                list.Add((pid, name));
            }

            if (list.Count == 0)
            {
                Console.WriteLine("No diagnosable .NET processes found.");
                return null;
            }

            Console.WriteLine("Select a process to attach to (enter number or PID), or empty to cancel:");
            for (int i = 0; i < list.Count; i++)
            {
                Console.WriteLine($"[{i}] {list[i].pid}\t{list[i].name}");
            }

            Console.Write("> ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) return null;

            // Try parse as index
            if (int.TryParse(input, out var n))
            {
                // If it's an index within range
                if (n >= 0 && n < list.Count) return list[n].pid;

                // Otherwise treat as PID
                return n;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Interactive selection failed: {ex.Message}");
            return null;
        }
    }
}
