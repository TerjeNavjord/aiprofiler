using System;
using DotAi.Utilities;

namespace DotAi.Commands;

public static class AttachCommand
{
    // Simple programmatic attach for the scaffold.
    public static void Invoke(int? pid, string? symPath, int timeout)
    {
        if (pid is null)
        {
            Console.Error.WriteLine("--pid is required");
            return;
        }

        var sessionId = SessionHelper.CreateSession(pid.Value, symPath, timeout);
        Console.WriteLine($"Session created: {sessionId}");
        Console.WriteLine($"Session directory: sessions/{sessionId}");
        Console.WriteLine("NOTE: This is a scaffolded attach — real diagnostic attach is not yet implemented.");
    }
}
