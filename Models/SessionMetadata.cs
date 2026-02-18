using Newtonsoft.Json;

namespace DotAi.Models;

internal class SessionMetadata
{
    [JsonProperty("sessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonProperty("pid")] public int? Pid { get; set; }
    [JsonProperty("symPath")] public string? SymPath { get; set; }
    [JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonProperty("artifacts")] public Dictionary<string, string> Artifacts { get; set; } = new();
}

internal class AnalysisResult
{
    [JsonProperty("sessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonProperty("traceFile")] public string TraceFile { get; set; } = string.Empty;
    [JsonProperty("question")] public string? Question { get; set; }
    [JsonProperty("generatedAt")] public DateTime GeneratedAt { get; set; }
    [JsonProperty("diagnosis")] public string Diagnosis { get; set; } = string.Empty;
    [JsonProperty("confidence")] public double Confidence { get; set; }
    [JsonProperty("evidence")] public string[] Evidence { get; set; } = Array.Empty<string>();
    [JsonProperty("suggestions")] public string[] Suggestions { get; set; } = Array.Empty<string>();
}
