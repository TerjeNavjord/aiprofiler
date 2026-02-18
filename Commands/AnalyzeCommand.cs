using System;
using System.IO;
using DotAi.Utilities;
using DotAi.Models;
using Newtonsoft.Json;

namespace DotAi.Commands;

internal static class AnalyzeCommand
{
    // Exposed small helper to be called from Program's AnalyzeSimple path.
    public static void Invoke(string traceOrSession, string? question)
    {
        string tracePath = traceOrSession;
        string sessionId = traceOrSession;

        if (Directory.Exists(SessionHelper.GetSessionDir(traceOrSession)))
        {
            // If the argument is a session id, try to find trace
            sessionId = traceOrSession;
            var sessDir = SessionHelper.GetSessionDir(sessionId);
            var candidate = Path.Combine(sessDir, "trace.nettrace");
            if (File.Exists(candidate)) tracePath = candidate;
            else
            {
                Console.Error.WriteLine($"No trace found in session {sessionId}");
                return;
            }
        }

        if (!File.Exists(tracePath))
        {
            Console.Error.WriteLine($"Trace file not found: {tracePath}");
            return;
        }

        // Simple stubbed analysis — in a real implementation we'd preprocess and call an LLM or local model
        var analysis = new AnalysisResult
        {
            SessionId = sessionId,
            TraceFile = tracePath,
            Question = question ?? "Why did CPU spike?",
            GeneratedAt = DateTime.UtcNow,
            Diagnosis = "Stub diagnosis: possible blocking synchronous I/O on request path.",
            Confidence = 0.42,
            Evidence = new[] { "Top stack frames: Controller.Handle -> Repo.Query -> SqlClient.Execute", "DB latency histogram increased 3x in selected interval" },
            Suggestions = new[] { "Consider using async DB APIs (ExecuteNonQueryAsync)", "Introduce batching or caching for frequent queries" }
        };

        var outDir = SessionHelper.GetSessionDir(sessionId);
        Directory.CreateDirectory(outDir);
        var outPath = Path.Combine(outDir, "analysis.json");
        File.WriteAllText(outPath, JsonConvert.SerializeObject(analysis, Formatting.Indented));

        Console.WriteLine($"Analysis (stub) written to: {outPath}");
        Console.WriteLine(analysis.Diagnosis);
    }
}
