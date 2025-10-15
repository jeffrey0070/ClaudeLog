using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeLog.Data;
using ClaudeLog.Data.Models;
using ClaudeLog.Data.Services;

namespace ClaudeLog.Hook.Codex;

class Program
{
    // Codex transcript hook
    // - stdin mode: reads { session_id, transcript_path, hook_event_name } from stdin (if Codex provides a per-turn hook)
    // - watcher mode: --watch <root> monitors JSONL transcripts and writes the last user→assistant pair on changes
    // The server expects a GUID SessionId; we normalize/derive one from filename or session_meta, or fall back to a
    // deterministic GUID based on the transcript path.

    // Safe initialization with null-coalescing
    private static readonly string StateDir =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/ClaudeLog";
    private static readonly string StatePath = Path.Combine(StateDir, "codex_state.json");

    private static readonly bool _debugEnabled = (Environment.GetEnvironmentVariable("CLAUDELOG_DEBUG") ?? "0") == "1";
    private static readonly bool _waitForDebugger = (Environment.GetEnvironmentVariable("CLAUDELOG_WAIT_FOR_DEBUGGER") ?? "0") == "1";
    private static readonly int _debuggerWaitSeconds = int.TryParse(
        Environment.GetEnvironmentVariable("CLAUDELOG_DEBUGGER_WAIT_SECONDS") ?? "60",
        out var seconds) ? seconds : 60;

    private static ConversationService? _conversationService;
    private static DiagnosticsService? _diagnosticsService;

    static async Task<int> Main(string[] args)
    {
        DbContext? dbContext = null;

        try
        {
            // Safe initialization of database services
            try
            {
                dbContext = new DbContext();
                _conversationService = new ConversationService(dbContext);
                _diagnosticsService = new DiagnosticsService(dbContext);

                // Enable debug logging if CLAUDELOG_DEBUG=1
                if (_debugEnabled)
                {
                    _diagnosticsService.DebugEnabled = true;
                }
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Codex] CRITICAL: Failed to initialize database services: {ex.Message}");
                return 1; // Exit with error code
            }

            await _diagnosticsService.WriteDiagnosticsAsync("Hook.Codex", "Hook.Codex started", LogLevel.Debug);

            // Wait for debugger attachment if requested
            if (_waitForDebugger)
            {
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Codex", $"Waiting for debugger - PID: {pid}", LogLevel.Debug);
                await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Codex] Waiting for debugger - PID: {pid}");

                for (int i = 0; i < _debuggerWaitSeconds; i++)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        await _diagnosticsService.WriteDiagnosticsAsync("Hook.Codex", "Debugger attached", LogLevel.Debug);
                        System.Diagnostics.Debugger.Break();
                        break;
                    }
                    await Task.Delay(1000);
                }

                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    await _diagnosticsService.WriteDiagnosticsAsync("Hook.Codex", "Debugger wait timeout", LogLevel.Debug);
                }
            }

            Directory.CreateDirectory(StateDir);
            await CheckpointStore.LoadAsync(StatePath);

            if (args.Length > 0 && string.Equals(args[0], "--watch", StringComparison.OrdinalIgnoreCase))
            {
                var root = args.Length > 1 ? args[1] : GuessDefaultCodexRoot();
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    await _diagnosticsService.WriteDiagnosticsAsync("Hook.Codex", $"Watch root not found: {root}", LogLevel.Error);
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
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Codex", "Unhandled exception in Main",
                LogLevel.Critical, $"{ex.Message}\n{ex.StackTrace}");
            await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Codex] CRITICAL: {ex.Message}");
            Console.WriteLine("{}");
            return 1; // Exit with error code
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
                    if (_debugEnabled) Console.WriteLine($"[VERBOSE] Processing: {path}");
                    var sessionId = await GetOrInferSessionIdAsync(path) ?? Guid.NewGuid().ToString();
                    await ProcessTranscriptOnceAsync(sessionId, path);
                    if (_debugEnabled) Console.WriteLine($"[VERBOSE] Completed: {path}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Watcher process failed for {path}: {ex.Message}");
                    await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Codex", $"Watcher process failed: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
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
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Codex", $"Transcript not found: {transcriptPath}", LogLevel.Error);
            return;
        }

        var pair = await TranscriptParser.ExtractLastPairAsync(transcriptPath);
        if (pair == null)
        {
            if (_debugEnabled) Console.WriteLine($"[VERBOSE] No Q&A pair found in {transcriptPath}");
            return;
        }

        var (question, response) = pair.Value;
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(response))
        {
            if (_debugEnabled) Console.WriteLine($"[VERBOSE] Empty Q&A in {transcriptPath}");
            return;
        }

        var hash = HashPair(question, response);
        if (CheckpointStore.IsDuplicate(transcriptPath, hash))
        {
            if (_debugEnabled) Console.WriteLine($"[VERBOSE] Duplicate detected (hash match): {transcriptPath}");
            return;
        }

        // Normalize/derive a GUID SessionId for server compatibility
        var sessionGuid = await EnsureGuidSessionIdAsync(sessionId, transcriptPath);
        await EnsureSessionAsync(sessionGuid);
        await WriteEntryAsync(sessionGuid, question.Trim(), response.Trim());

        if (_debugEnabled) Console.WriteLine($"[VERBOSE] Wrote entry for session {sessionGuid}");

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

    private static async Task EnsureSessionAsync(string sessionId)
    {
        try
        {
            await _conversationService!.EnsureSessionAsync(sessionId, "Codex");
        }
        catch (Exception ex)
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Codex", $"Failed to ensure session: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
        }
    }

    private static async Task WriteEntryAsync(string sessionId, string question, string response)
    {
        try
        {
            var entryId = await _conversationService!.WriteEntryAsync(sessionId, question, response);
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Codex", $"Entry written successfully (ID: {entryId})", LogLevel.Debug);
        }
        catch (Exception ex)
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Codex", $"Failed to write entry: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
        }
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
