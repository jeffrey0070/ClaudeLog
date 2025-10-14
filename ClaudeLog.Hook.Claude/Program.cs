using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeLog.Data;
using ClaudeLog.Data.Models;
using ClaudeLog.Data.Services;

namespace ClaudeLog.Hook.Claude;

/// <summary>
/// Claude Code hook that captures conversation transcripts and logs them to ClaudeLog database.
/// This hook is triggered by the "Stop" event after each Claude Code conversation turn.
/// </summary>
class Program
{
    // Safe initialization with null-coalescing
    private static readonly bool _debugEnabled = (Environment.GetEnvironmentVariable("CLAUDELOG_DEBUG") ?? "0") == "1";
    private static readonly bool _waitForDebugger = (Environment.GetEnvironmentVariable("CLAUDELOG_WAIT_FOR_DEBUGGER") ?? "0") == "1";
    private static readonly int _debuggerWaitSeconds = int.TryParse(
        Environment.GetEnvironmentVariable("CLAUDELOG_DEBUGGER_WAIT_SECONDS") ?? "60",
        out var seconds) ? seconds : 60;

    static async Task<int> Main(string[] args)
    {
        DbContext? dbContext = null;
        LoggingService? loggingService = null;

        try
        {
            // Safe initialization of database services
            try
            {
                dbContext = new DbContext();
                loggingService = new LoggingService(dbContext);
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Claude] CRITICAL: Failed to initialize database services: {ex.Message}");
                return 1; // Exit with error code
            }

            await LogAsync(loggingService, LogLevel.Debug, "Hook.Claude started");

            // Wait for debugger attachment if requested
            if (_waitForDebugger)
            {
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                await LogAsync(loggingService, LogLevel.Debug, $"Waiting for debugger - PID: {pid}");
                await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Claude] Waiting for debugger - PID: {pid}");

                for (int i = 0; i < _debuggerWaitSeconds; i++)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        await LogAsync(loggingService, LogLevel.Debug, "Debugger attached");
                        System.Diagnostics.Debugger.Break();
                        break;
                    }
                    await Task.Delay(1000);
                }

                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    await LogAsync(loggingService, LogLevel.Debug, "Debugger wait timeout");
                }
            }

            // Read hook input from stdin (JSON provided by Claude Code)
            using var reader = new StreamReader(Console.OpenStandardInput());
            var json = await reader.ReadToEndAsync();

            await LogAsync(loggingService, LogLevel.Trace, "Received hook input", json);

            var hookInput = JsonSerializer.Deserialize<HookInput>(json);

            if (hookInput?.TranscriptPath != null && hookInput.SessionId != null)
            {
                await LogAsync(loggingService, LogLevel.Debug, $"Processing transcript for session {hookInput.SessionId}");
                await ProcessTranscriptAsync(loggingService, hookInput.SessionId, hookInput.TranscriptPath);
            }
            else
            {
                await LogAsync(loggingService, LogLevel.Warning, "Missing required hook input fields",
                    $"SessionId: {hookInput?.SessionId}, TranscriptPath: {hookInput?.TranscriptPath}");
            }

            // Output empty JSON (hook doesn't modify anything)
            Console.WriteLine("{}");
            await LogAsync(loggingService, LogLevel.Debug, "Hook.Claude completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            // Log critical error
            if (loggingService != null)
            {
                await loggingService.LogCriticalAsync("Hook.Claude", "Unhandled exception in Main",
                    $"{ex.Message}\n{ex.StackTrace}");
            }
            await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Claude] CRITICAL: {ex.Message}");

            // Always output {} so Claude Code doesn't fail
            Console.WriteLine("{}");
            return 1; // Exit with error code
        }
    }

    /// <summary>
    /// Processes the Claude Code transcript file and extracts the last Q&A pair.
    /// Creates a session for the conversation and logs the conversation entry.
    /// </summary>
    static async Task ProcessTranscriptAsync(LoggingService loggingService, string sessionId, string transcriptPath)
    {
        // Expand environment variables in path (handles %USERPROFILE% etc.)
        transcriptPath = Environment.ExpandEnvironmentVariables(transcriptPath);

        if (!File.Exists(transcriptPath))
        {
            await loggingService.LogErrorAsync("Hook.Claude", $"Transcript file not found: {transcriptPath}");
            return;
        }

        // Read last Q&A from transcript (JSONL format)
        var (question, response) = await ReadLastInteractionAsync(loggingService, transcriptPath);

        if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(response))
        {
            await LogAsync(loggingService, LogLevel.Debug, "No Q&A pair found in transcript");
            return;
        }

        // Ensure session exists (creates if not exists)
        await EnsureSessionAsync(loggingService, sessionId);

        // Log the conversation entry via database
        await LogEntryAsync(loggingService, sessionId, question, response);
    }

    /// <summary>
    /// Reads the JSONL transcript file and extracts the last user question and assistant response.
    /// Claude Code v2.0.8+ uses format: {"type":"user/assistant", "message":{"content":[...]}}
    /// </summary>
    static async Task<(string? question, string? response)> ReadLastInteractionAsync(LoggingService loggingService, string transcriptPath)
    {
        try
        {
            await LogAsync(loggingService, LogLevel.Trace, $"Reading transcript from: {transcriptPath}");
            var lines = await File.ReadAllLinesAsync(transcriptPath);
            await LogAsync(loggingService, LogLevel.Trace, $"Found {lines.Length} lines in transcript");

            var messages = new List<TranscriptMessage>();

            // Parse JSONL format (one JSON object per line)
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var msg = JsonSerializer.Deserialize<TranscriptMessage>(line);
                    if (msg != null)
                    {
                        messages.Add(msg);
                    }
                }
            }

            await LogAsync(loggingService, LogLevel.Trace, $"Parsed {messages.Count} messages");

            string? question = null;
            string? response = null;

            // Read backwards to find last user message and assistant response
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var msg = messages[i];

                if (msg.Type == "assistant" && msg.Message != null && response == null)
                {
                    response = ExtractTextContent(msg.Message.Content);
                    await LogAsync(loggingService, LogLevel.Trace, $"Found assistant response (length: {response?.Length ?? 0})");
                }
                else if (msg.Type == "user" && msg.Message != null && question == null)
                {
                    question = ExtractTextContent(msg.Message.Content);
                    await LogAsync(loggingService, LogLevel.Trace, $"Found user question (length: {question?.Length ?? 0})");
                }

                if (question != null && response != null)
                {
                    break;
                }
            }

            return (question, response);
        }
        catch (Exception ex)
        {
            await loggingService.LogErrorAsync("Hook.Claude", $"Error reading transcript: {ex.Message}", ex.StackTrace ?? "");
            return (null, null);
        }
    }

    static string? ExtractTextContent(JsonElement? content)
    {
        if (content == null)
            return null;

        var parts = new List<string>();

        if (content.Value.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.Value.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) && type.GetString() == "text")
                {
                    if (block.TryGetProperty("text", out var text))
                    {
                        parts.Add(text.GetString() ?? "");
                    }
                }
            }
            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }
        else if (content.Value.ValueKind == JsonValueKind.String)
        {
            return content.Value.GetString();
        }

        return null;
    }

    static async Task EnsureSessionAsync(LoggingService loggingService, string sessionId)
    {
        try
        {
            await loggingService.EnsureSessionAsync(sessionId, "ClaudeCode");
        }
        catch (Exception ex)
        {
            await loggingService.LogErrorAsync("Hook.Claude", $"Failed to ensure session: {ex.Message}", ex.StackTrace ?? "");
        }
    }

    static async Task LogEntryAsync(LoggingService loggingService, string sessionId, string question, string response)
    {
        try
        {
            await LogAsync(loggingService, LogLevel.Debug, $"Logging entry for session: {sessionId}");
            var (success, entryId) = await loggingService.LogEntryAsync(sessionId, question, response);

            if (success)
            {
                await LogAsync(loggingService, LogLevel.Info, $"Entry logged successfully (ID: {entryId})");
            }
            else
            {
                await loggingService.LogWarningAsync("Hook.Claude", "Failed to log entry (unknown reason)");
            }
        }
        catch (Exception ex)
        {
            await loggingService.LogErrorAsync("Hook.Claude", $"Failed to log entry: {ex.Message}", ex.StackTrace ?? "");
        }
    }

    /// <summary>
    /// Unified logging method that logs to both database and debug file
    /// </summary>
    static async Task LogAsync(LoggingService loggingService, LogLevel level, string message, string? detail = null)
    {
        // Always log to database
        await loggingService.LogAsync("Hook.Claude", message, level, detail);

        // Also log to debug file if enabled
        if (_debugEnabled)
        {
            await WriteDebugLogAsync($"[{level}] {message}" + (detail != null ? $"\n  Detail: {detail}" : ""));
        }
    }

    /// <summary>
    /// Writes debug log entries to file when CLAUDELOG_DEBUG=1 environment variable is set.
    /// Log file location: %USERPROFILE%\.claudelog\hook-claude-debug.log
    /// </summary>
    static async Task WriteDebugLogAsync(string message)
    {
        try
        {
            var debugLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claudelog",
                "hook-claude-debug.log"
            );

            var logDir = Path.GetDirectoryName(debugLogPath);
            if (logDir != null && !Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var logMessage = $"[{timestamp}] {message}\n";

            await File.AppendAllTextAsync(debugLogPath, logMessage);
        }
        catch
        {
            // Swallow debug logging errors to avoid breaking the hook
        }
    }
}

class HookInput
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; set; }

    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; set; }
}

class TranscriptMessage
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("message")]
    public MessageContent? Message { get; set; }
}

class MessageContent
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }
}
