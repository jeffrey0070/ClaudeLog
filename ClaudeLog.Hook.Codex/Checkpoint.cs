using System.Text.Json;

namespace ClaudeLog.Hook.Codex;

class CheckpointRecord
{
    public long LastSize { get; set; }
    public string LastHash { get; set; } = "";
    public DateTime LastEntryAt { get; set; }
}

// Minimal JSON checkpoint store keyed by transcript path to avoid duplicate inserts.
static class CheckpointStore
{
    private static readonly object Gate = new();
    private static Dictionary<string, CheckpointRecord> _map = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsDuplicate(string path, string hash)
    {
        lock (Gate)
        {
            if (_map.TryGetValue(path, out var rec))
            {
                return string.Equals(rec.LastHash, hash, StringComparison.Ordinal);
            }
            return false;
        }
    }

    public static void Update(string path, CheckpointRecord rec)
    {
        lock (Gate) { _map[path] = rec; }
    }

    public static async Task LoadAsync(string statePath)
    {
        try
        {
            if (!File.Exists(statePath)) return;
            var json = await File.ReadAllTextAsync(statePath);
            var tmp = JsonSerializer.Deserialize<Dictionary<string, CheckpointRecord>>(json);
            if (tmp != null)
            {
                lock (Gate) _map = new Dictionary<string, CheckpointRecord>(tmp, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch { }
    }

    public static async Task SaveAsync(string statePath)
    {
        try
        {
            var json = JsonSerializer.Serialize(_map, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(statePath, json);
        }
        catch { }
    }
}
