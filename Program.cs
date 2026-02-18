using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Microsoft.Diagnostics.NETCore.Client;
using DotAi.Commands;

// Minimal CLI bootstrap without System.CommandLine to avoid heavy deps in the scaffold.
// We parse a very small set of commands manually to allow running basic flows.

    if (args.Length == 0)
    {
        Console.WriteLine("dotai — .NET AI profiler/debugger CLI (scaffold)");
    Console.WriteLine("Available commands: attach, trace record, analyze, ps");
        return;
    }

try
{
    var cmd = args[0];
    switch (cmd)
    {
        case "attach":
            // Expect: attach --pid <pid> [--sym-path <path>] [--timeout <s>]
            AttachSimple(args[1..]);
            break;
        case "trace":
            // trace record [--session id] [--duration n] [--output path] [--pid <pid>]
            if (args.Length >= 2 && args[1] == "record") TraceSimple(args[2..]);
            else Console.WriteLine("Unknown trace subcommand. Use: trace record");
            break;
        case "ps":
            // ps [--json] [--details]
            bool pj = argvContains(args, "--json");
            bool pdet = argvContains(args, "--details");
            DotAi.Commands.PsCommand.Invoke(pj, pdet);
            break;
    case "analyze":
        // analyze <traceOrSession> [--question "..."]
            if (args.Length >= 2 && args[1] == "postprocess")
            {
                if (args.Length >= 3) DotAi.Commands.PostProcessCommand.Invoke(args[2]);
                else Console.WriteLine("Usage: analyze postprocess <sessionId>");
            }
            else
            {
                AnalyzeSimple(args[1..]);
            }
            break;
        case "debug":
            // debug attach|set-breakpoint|install-adapter
            if (args.Length >= 2)
            {
                var sub = args[1];
                switch (sub)
                {
                    case "attach":
                        // debug attach --session <id> --adapter <path> --pid <pid>
                        {
                            string? sid = null; string? adapter = null; int? pid = null;
                            for (int i = 2; i < args.Length; i++)
                            {
                                switch (args[i])
                                {
                                    case "--session": sid = args[++i]; break;
                                    case "--adapter": adapter = args[++i]; break;
                                    case "--pid": pid = int.Parse(args[++i]); break;
                                }
                            }
                            if (sid == null) sid = DotAi.Utilities.SessionHelper.CreateSession(pid, null, 30);
                            if (adapter == null) { Console.WriteLine("--adapter <path> required"); break; }
                            if (pid == null) { Console.WriteLine("--pid <pid> required"); break; }
                            DotAi.Commands.DebugCommand.InvokeAttach(sid, adapter, pid.Value);
                        }
                        break;
                    case "set-breakpoint":
                        // debug set-breakpoint --session <id> --adapter <path> --file <file> --line <n>
                        {
                            string? sid = null; string? adapter = null; string? file = null; int line = 0;
                            for (int i = 2; i < args.Length; i++)
                            {
                                switch (args[i])
                                {
                                    case "--session": sid = args[++i]; break;
                                    case "--adapter": adapter = args[++i]; break;
                                    case "--file": file = args[++i]; break;
                                    case "--line": line = int.Parse(args[++i]); break;
                                }
                            }
                            if (sid == null) sid = DotAi.Utilities.SessionHelper.CreateSession();
                            if (adapter == null || file == null || line == 0) { Console.WriteLine("Usage: debug set-breakpoint --adapter <path> --file <path> --line <n>"); break; }
                            DotAi.Commands.DebugCommand.InvokeSetBreakpoint(sid, adapter, file, line);
                        }
                        break;
                    case "install-adapter":
                        // debug install-adapter netcoredbg|vsdbg
                        if (args.Length >= 3)
                        {
                            var which = args[2];
                            if (string.Equals(which, "netcoredbg", StringComparison.OrdinalIgnoreCase))
                            {
                                // call installer synchronously
                                DotAi.Commands.DebugCommand.InstallNetCoreDbg().GetAwaiter().GetResult();
                            }
                            else Console.WriteLine("Unsupported adapter. Supported: netcoredbg");
                        }
                        else Console.WriteLine("Usage: debug install-adapter netcoredbg");
                        break;
                    default:
                        Console.WriteLine("Unknown debug subcommand: " + sub);
                        break;
                }
            }
            break;
        default:
            Console.WriteLine($"Unknown command: {cmd}");
            break;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
}

static void AttachSimple(string[] argv)
{
    int? pid = null; string? sym = null; int timeout = 30;
    for (int i = 0; i < argv.Length; i++)
    {
        switch (argv[i])
        {
            case "--pid": pid = int.Parse(argv[++i]); break;
            case "--sym-path": sym = argv[++i]; break;
            case "--timeout": timeout = int.Parse(argv[++i]); break;
        }
    }

    var sessionId = DotAi.Utilities.SessionHelper.CreateSession(pid, sym, timeout);
    Console.WriteLine($"Session created: {sessionId}");
}

static void TraceSimple(string[] argv)
{
    string? session = null; int duration = 30; string? output = null;
    int? pid = null;
    for (int i = 0; i < argv.Length; i++)
    {
        switch (argv[i])
        {
            case "--session": session = argv[++i]; break;
            case "--duration": duration = int.Parse(argv[++i]); break;
            case "--output": output = argv[++i]; break;
            case "--pid": pid = int.Parse(argv[++i]); break;
        }
    }

    // Reuse TraceCommand logic by creating a session and writing a stub trace
    var sessionId = session ?? DotAi.Utilities.SessionHelper.CreateSession();
    var sessDir = DotAi.Utilities.SessionHelper.GetSessionDir(sessionId);
    Directory.CreateDirectory(sessDir);
    var outPath = output ?? Path.Combine(sessDir, "trace.nettrace");
    // If no PID provided, offer interactive selection
    if (pid is null)
    {
        Console.WriteLine("No --pid specified. You can pick from a list of diagnosable processes.");
        var chosen = DotAi.Utilities.Interactive.ChoosePidInteractive();
        if (chosen is null)
        {
            Console.WriteLine("No PID chosen — aborting trace.");
            return;
        }

        pid = chosen;
    }

    // invoke TraceCommand which will validate PID and either record or emit helpful errors
    DotAi.Commands.TraceCommand.Invoke(sessionId, duration, output, pid);
}

static bool argvContains(string[] argv, string opt) => argv.Any(a => string.Equals(a, opt, StringComparison.OrdinalIgnoreCase));

static void AnalyzeSimple(string[] argv)
{
    if (argv.Length == 0) { Console.Error.WriteLine("analyze requires a trace or session id"); return; }
    var traceOrSession = argv[0];
    string? question = null;
    for (int i = 1; i < argv.Length; i++) if (argv[i] == "--question") question = argv[++i];

    // Invoke the analyze helper
    DotAi.Commands.AnalyzeCommand.Invoke(traceOrSession, question);
}
