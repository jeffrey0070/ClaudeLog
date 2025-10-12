using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeLog.Data;
using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.Hook.Codex;

class Program
{
    // Codex transcript hook
    // - stdin mode: reads { session_id, transcript_path, hook_event_name } from stdin (if Codex provides a per-turn hook)
    // - watcher mode: --watch <root> monitors JSONL transcripts and logs the last user→assistant pair on changes
    // The server expects a GUID SectionId; we normalize/derive one from filename or session_meta, or fall back to a
    // deterministic GUID based on the transcript path.

    private static readonly DbContext _dbContext = new();
    private static readonly SectionRepository _sectionRepository = new(_dbContext);
    private static readonly EntryRepository _entryRepository = new(_dbContext);
    private static readonly ErrorRepository _errorRepository = new(_dbContext);

    private static readonly string StateDir =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/ClaudeLog";
    private static readonly string StatePath = Path.Combine(StateDir, "codex_state.json");

    private static bool VerboseLogging =>
        string.Equals(Environment.GetEnvironmentVariable("CLAUDELOG_HOOK_LOGLEVEL"), "verbose", StringComparison.OrdinalIgnoreCase);

    static async Task<int> Main(string[] args)
    {
        try
        {
            Directory.CreateDirectory(StateDir);
            await CheckpointStore.LoadAsync(StatePath);

            if (args.Length > 0 && string.Equals(args[0], "--watch", StringComparison.OrdinalIgnoreCase))
            {
                var root = args.Length > 1 ? args[1] : GuessDefaultCodexRoot();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    await LogErrorAsync("Hook.Codex", $"Watch root not found: {root}", "");
                    Console.WriteLine("{}");
                    return 0;
                }
                await RunWatcherAsync(root);
                return 0;
            }

            // stdin mode (preferred)
            using var reader = new StreamReader(Console.OpenStandardInput());
            var input = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(input))
            {
                var hook = JsonSerializer.Deserialize<HookInput>(input);
                if (hook?.TranscriptPath != null && hook.SessionId != null)
                {
                    await ProcessTranscriptOnceAsync(hook.SessionId, hook.TranscriptPath);
                }
            }

            Console.WriteLine("{}");
            return 0;
        }
        catch (Exception ex)
        {
            await LogErrorAsync("Hook.Codex", ex.Message, ex.StackTrace ?? "");
            Console.WriteLine("{}");
            return 0;
        }
        finally
        {
            await CheckpointStore.SaveAsync(StatePath);
        }
    }

    private static async Task RunWatcherAsync(string root)
    {
        using var fsw = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            Filter = "*.jsonl",
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size
        };

        var pending = new Dictionary<string, DateTime>();
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\nStopping Codex watcher...");
            cts.Cancel();
        };

        Console.WriteLine($"Watching {root} for Codex transcripts...");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.WriteLine();

        fsw.Created += (s, e) => { lock (pending) pending[e.FullPath] = DateTime.UtcNow; };
        fsw.Changed += (s, e) => { lock (pending) pending[e.FullPath] = DateTime.UtcNow; };
        fsw.Renamed += (s, e) => { lock (pending) pending[e.FullPath] = DateTime.UtcNow; };
        fsw.EnableRaisingEvents = true;

        // simple debounce loop
        while (!cts.IsCancellationRequested)
        {
            await Task.Delay(300, cts.Token).ContinueWith(_ => { });
            List<string> toProcess;
            lock (pending)
            {
                var now = DateTime.UtcNow;
                toProcess = pending
                    .Where(kv => (now - kv.Value).TotalMilliseconds >= 250)
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var k in toProcess) pending.Remove(k);
            }

            foreach (var path in toProcess)
            {
                try
                {
                    if (VerboseLogging) Console.WriteLine($"[VERBOSE] Processing: {path}");
                    var sessionId = await GetOrInferSessionIdAsync(path) ?? Guid.NewGuid().ToString();
                    await ProcessTranscriptOnceAsync(sessionId, path);
                    if (VerboseLogging) Console.WriteLine($"[VERBOSE] Completed: {path}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Watcher process failed for {path}: {ex.Message}");
                    await LogErrorAsync("Hook.Codex", $"Watcher process failed: {ex.Message}", ex.StackTrace ?? "");
                }
            }
        }
        Console.WriteLine("Watcher stopped.");
    }

    private static async Task ProcessTranscriptOnceAsync(string sessionId, string transcriptPath)
    {
        transcriptPath = Environment.ExpandEnvironmentVariables(transcriptPath);
        if (!File.Exists(transcriptPath))
        {
            await LogErrorAsync("Hook.Codex", $"Transcript not found: {transcriptPath}", "");
            return;
        }

        var pair = await TranscriptParser.ExtractLastPairAsync(transcriptPath);
        if (pair == null)
        {
            if (VerboseLogging) Console.WriteLine($"[VERBOSE] No Q&A pair found in {transcriptPath}");
            return;
        }

        var (question, response) = pair.Value;
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(response))
        {
            if (VerboseLogging) Console.WriteLine($"[VERBOSE] Empty Q&A in {transcriptPath}");
            return;
        }

        var hash = HashPair(question, response);
        if (CheckpointStore.IsDuplicate(transcriptPath, hash))
        {
            if (VerboseLogging) Console.WriteLine($"[VERBOSE] Duplicate detected (hash match): {transcriptPath}");
            return;
        }

        // Normalize/derive a GUID SectionId for server compatibility
        var sectionGuid = await EnsureGuidSessionIdAsync(sessionId, transcriptPath);
        await EnsureSectionAsync(sectionGuid);
        await LogEntryAsync(sectionGuid, question.Trim(), response.Trim());

        if (VerboseLogging) Console.WriteLine($"[VERBOSE] Logged entry for session {sectionGuid}");

        CheckpointStore.Update(transcriptPath, new CheckpointRecord
        {
            LastSize = new FileInfo(transcriptPath).Length,
            LastHash = hash,
            LastEntryAt = DateTime.Now
        });
    }

    private static string HashPair(string q, string r)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(q + "\n" + r));
        return Convert.ToHexString(bytes);
    }

    private static string? GuessDefaultCodexRoot()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("CODEX_TRANSCRIPT_PATH"),
            Path.Combine(home, ".codex", "sessions"),
            Path.Combine(home, ".codex"),
            Path.Combine(home, ".chatgpt", "codex"),
        };
        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p));
    }

    private static string? InferSessionIdFromPath(string path)
    {
        // Try to pull a GUID or filename stem as session ID
        var name = Path.GetFileNameWithoutExtension(path);
        if (Guid.TryParse(name, out var g)) return g.ToString();
        var tokens = name.Split('-');
        if (tokens.Length > 0 && Guid.TryParse(tokens[^1], out var g2)) return g2.ToString();
        return name;
    }

    private static async Task<string?> GetOrInferSessionIdAsync(string transcriptPath)
    {
        var fromName = InferSessionIdFromPath(transcriptPath);
        if (!string.IsNullOrWhiteSpace(fromName)) return fromName;
        try
        {
            using var sr = new StreamReader(transcriptPath);
            for (int i = 0; i < 5; i++)
            {
                var line = await sr.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var t) && t.GetString() == "session_meta")
                {
                    if (root.TryGetProperty("payload", out var payload) && payload.TryGetProperty("id", out var idEl))
                    {
                        var id = idEl.GetString();
                        if (!string.IsNullOrWhiteSpace(id)) return id;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static async Task<string> EnsureGuidSessionIdAsync(string sessionId, string transcriptPath)
    {
        if (Guid.TryParse(sessionId, out _)) return sessionId;
        var fromFile = await GetOrInferSessionIdAsync(transcriptPath);
        if (!string.IsNullOrWhiteSpace(fromFile) && Guid.TryParse(fromFile, out var g)) return g.ToString();
        return DeterministicGuidFromString(transcriptPath).ToString();
    }

    private static Guid DeterministicGuidFromString(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }

    private static async Task EnsureSectionAsync(string sessionId)
    {
        try
        {
            var request = new CreateSectionRequest("Codex", sessionId, null);
            await _sectionRepository.CreateAsync(request);
        }
        catch { }
    }

    private static async Task LogEntryAsync(string sessionId, string question, string response)
    {
        try
        {
            var request = new CreateEntryRequest(sessionId, question, response);
            await _entryRepository.CreateAsync(request);
        }
        catch (Exception ex)
        {
            await LogErrorAsync("Hook.Codex", $"Failed to log entry: {ex.Message}", ex.StackTrace ?? "");
        }
    }

    private static async Task LogErrorAsync(string source, string message, string detail)
    {
        try
        {
            var request = new LogErrorRequest(source, message, detail, null, null, null, null);
            await _errorRepository.LogErrorAsync(request);
        }
        catch { }
    }
}

public record struct Pair(string Question, string Response);

class HookInput
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; set; }

    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; set; }
}
