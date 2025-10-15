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
                await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Claude] CRITICAL: Failed to initialize database services: {ex.Message}");
                return 1; // Exit with error code
            }

            await _diagnosticsService.WriteDiagnosticsAsync("Hook.Claude", "Hook.Claude started", LogLevel.Debug);

            // Wait for debugger attachment if requested
            if (_waitForDebugger)
            {
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Claude", $"Waiting for debugger - PID: {pid}", LogLevel.Debug);
                await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Claude] Waiting for debugger - PID: {pid}");

                for (int i = 0; i < _debuggerWaitSeconds; i++)
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        await _diagnosticsService.WriteDiagnosticsAsync("Hook.Claude", "Debugger attached", LogLevel.Debug);
                        System.Diagnostics.Debugger.Break();
                        break;
                    }
                    await Task.Delay(1000);
                }

                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    await _diagnosticsService.WriteDiagnosticsAsync("Hook.Claude", "Debugger wait timeout", LogLevel.Debug);
                }
            }

            // Read hook input from stdin (JSON provided by Claude Code)
            using var reader = new StreamReader(Console.OpenStandardInput());
            var json = await reader.ReadToEndAsync();

            await _diagnosticsService.WriteDiagnosticsAsync("Hook.Claude", "Received hook input", LogLevel.Trace, json);

            var hookInput = JsonSerializer.Deserialize<HookInput>(json);

            if (hookInput?.TranscriptPath != null && hookInput.SessionId != null)
            {
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Claude", $"Processing transcript for session {hookInput.SessionId}", LogLevel.Debug);
                await ProcessTranscriptAsync(hookInput.SessionId, hookInput.TranscriptPath);
            }
            else
            {
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Claude", "Missing required hook input fields", LogLevel.Warning,
                    $"SessionId: {hookInput?.SessionId}, TranscriptPath: {hookInput?.TranscriptPath}");
            }

            // Output empty JSON (hook doesn't modify anything)
            Console.WriteLine("{}");
            await _diagnosticsService.WriteDiagnosticsAsync("Hook.Claude", "Hook.Claude completed successfully", LogLevel.Debug);
            return 0;
        }
        catch (Exception ex)
        {
            // Log critical error
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", "Unhandled exception in Main",
                LogLevel.Critical, $"{ex.Message}\n{ex.StackTrace}");
            await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Claude] CRITICAL: {ex.Message}");

            // Always output {} so Claude Code doesn't fail
            Console.WriteLine("{}");
            return 1; // Exit with error code
        }
    }

    /// <summary>
    /// Processes the Claude Code transcript file and extracts the last Q&A pair.
    /// Creates a session for the conversation and writes the conversation entry.
    /// </summary>
    static async Task ProcessTranscriptAsync(string sessionId, string transcriptPath)
    {
        // Expand environment variables in path (handles %USERPROFILE% etc.)
        transcriptPath = Environment.ExpandEnvironmentVariables(transcriptPath);

        if (!File.Exists(transcriptPath))
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Transcript file not found: {transcriptPath}", LogLevel.Error);
            return;
        }

        // Read last Q&A from transcript (JSONL format)
        var (question, response) = await ReadLastInteractionAsync(transcriptPath);

        if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(response))
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", "No Q&A pair found in transcript", LogLevel.Debug);
            return;
        }

        // Ensure session exists (creates if not exists)
        await EnsureSessionAsync(sessionId);

        // Write the conversation entry to database
        await WriteEntryAsync(sessionId, question, response);
    }

    /// <summary>
    /// Reads the JSONL transcript file and extracts the last user question and assistant response.
    /// Claude Code v2.0.8+ uses format: {"type":"user/assistant", "message":{"content":[...]}}
    /// </summary>
    static async Task<(string? question, string? response)> ReadLastInteractionAsync(string transcriptPath)
    {
        try
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Reading transcript from: {transcriptPath}", LogLevel.Trace);
            var lines = await File.ReadAllLinesAsync(transcriptPath);
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Found {lines.Length} lines in transcript", LogLevel.Trace);

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

            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Parsed {messages.Count} messages", LogLevel.Trace);

            string? question = null;
            string? response = null;

            // Read backwards to find last user message and assistant response
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var msg = messages[i];

                if (msg.Type == "assistant" && msg.Message != null && response == null)
                {
                    response = ExtractTextContent(msg.Message.Content);
                    await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Found assistant response (length: {response?.Length ?? 0})", LogLevel.Trace);
                }
                else if (msg.Type == "user" && msg.Message != null && question == null)
                {
                    question = ExtractTextContent(msg.Message.Content);
                    await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Found user question (length: {question?.Length ?? 0})", LogLevel.Trace);
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
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Error reading transcript: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
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

    static async Task EnsureSessionAsync(string sessionId)
    {
        try
        {
            await _conversationService!.EnsureSessionAsync(sessionId, "ClaudeCode");
        }
        catch (Exception ex)
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Failed to ensure session: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
        }
    }

    static async Task WriteEntryAsync(string sessionId, string question, string response)
    {
        try
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Writing entry for session: {sessionId}", LogLevel.Debug);
            var entryId = await _conversationService!.WriteEntryAsync(sessionId, question, response);
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Entry written successfully (ID: {entryId})", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Claude", $"Failed to write entry: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
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
