using DotAi.Models;
using Newtonsoft.Json;

namespace DotAi.Utilities;

internal static class SessionHelper
{
    private const string SessionsRoot = "sessions";

    public static string CreateSession(int? pid = null, string? symPath = null, int timeoutSeconds = 30)
    {
        Directory.CreateDirectory(SessionsRoot);
        var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("n").Substring(0, 6);
        var meta = new SessionMetadata
        {
            SessionId = id,
            Pid = pid,
            SymPath = symPath,
            CreatedAt = DateTime.UtcNow,
        };

        var dir = GetSessionDir(id);
        Directory.CreateDirectory(dir);
        var metaPath = Path.Combine(dir, "metadata.json");
        File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta, Formatting.Indented));
        return id;
    }

    public static string GetSessionDir(string sessionId) => Path.Combine(SessionsRoot, sessionId);

    public static void AddArtifact(string sessionId, string key, string path)
    {
        var dir = GetSessionDir(sessionId);
        var metaPath = Path.Combine(dir, "metadata.json");
        if (!File.Exists(metaPath)) return;
        var json = File.ReadAllText(metaPath);
        var meta = JsonConvert.DeserializeObject<SessionMetadata>(json)!;
        meta.Artifacts[key] = path;
        File.WriteAllText(metaPath, JsonConvert.SerializeObject(meta, Formatting.Indented));
    }
}
