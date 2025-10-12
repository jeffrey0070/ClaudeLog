using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeLog.Data;
using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.Hook.Claude;

/// <summary>
/// Claude Code hook that captures conversation transcripts and logs them to ClaudeLog database.
/// This hook is triggered by the "Stop" event after each Claude Code conversation turn.
/// </summary>
class Program
{
    private static readonly DbContext _dbContext = new();
    private static readonly SectionRepository _sectionRepository = new(_dbContext);
    private static readonly EntryRepository _entryRepository = new(_dbContext);
    private static readonly ErrorRepository _errorRepository = new(_dbContext);

    static async Task Main(string[] args)
    {
        try
        {
            // Read hook input from stdin (JSON provided by Claude Code)
            using var reader = new StreamReader(Console.OpenStandardInput());
            var json = await reader.ReadToEndAsync();

            var hookInput = JsonSerializer.Deserialize<HookInput>(json);

            if (hookInput?.TranscriptPath != null && hookInput.SessionId != null)
            {
                await ProcessTranscriptAsync(hookInput.SessionId, hookInput.TranscriptPath);
            }

            // Output empty JSON (hook doesn't modify anything)
            Console.WriteLine("{}");
        }
        catch (Exception ex)
        {
            // Log error but don't fail the hook (allows Claude Code to continue)
            await LogErrorAsync("Hook.Claude", ex.Message, ex.StackTrace ?? "");
            Console.WriteLine("{}");
        }
    }

    /// <summary>
    /// Processes the Claude Code transcript file and extracts the last Q&A pair.
    /// Creates a section for the session and logs the conversation entry.
    /// </summary>
    static async Task ProcessTranscriptAsync(string sessionId, string transcriptPath)
    {
        // Expand environment variables in path (handles %USERPROFILE% etc.)
        transcriptPath = Environment.ExpandEnvironmentVariables(transcriptPath);

        if (!File.Exists(transcriptPath))
        {
            await LogErrorAsync("Hook.Claude", $"Transcript file not found: {transcriptPath}", "");
            return;
        }

        // Read last Q&A from transcript (JSONL format)
        var (question, response) = await ReadLastInteractionAsync(transcriptPath);

        if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(response))
        {
            return; // Nothing to log
        }

        // Ensure section exists for this session (creates if not exists)
        await EnsureSectionAsync(sessionId);

        // Log the conversation entry via database
        await LogEntryAsync(sessionId, question, response);
    }

    /// <summary>
    /// Reads the JSONL transcript file and extracts the last user question and assistant response.
    /// Claude Code v2.0.8+ uses format: {"type":"user/assistant", "message":{"content":[...]}}
    /// </summary>
    static async Task<(string? question, string? response)> ReadLastInteractionAsync(string transcriptPath)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(transcriptPath);
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

            string? question = null;
            string? response = null;

            // Read backwards to find last user message and assistant response
            for (int i = messages.Count - 1; i >= 0; i--)
            {
                var msg = messages[i];

                if (msg.Type == "assistant" && msg.Message != null && response == null)
                {
                    response = ExtractTextContent(msg.Message.Content);
                }
                else if (msg.Type == "user" && msg.Message != null && question == null)
                {
                    question = ExtractTextContent(msg.Message.Content);
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
            await LogErrorAsync("Hook.Claude", $"Error reading transcript: {ex.Message}", ex.StackTrace ?? "");
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

    static async Task EnsureSectionAsync(string sessionId)
    {
        try
        {
            var request = new CreateSectionRequest("ClaudeCode", sessionId, null);
            await _sectionRepository.CreateAsync(request);
        }
        catch
        {
            // Swallow errors - section might already exist
        }
    }

    static async Task LogEntryAsync(string sessionId, string question, string response)
    {
        try
        {
            var request = new CreateEntryRequest(sessionId, question, response);
            await _entryRepository.CreateAsync(request);
        }
        catch (Exception ex)
        {
            await LogErrorAsync("Hook.Claude", $"Failed to log entry: {ex.Message}", ex.StackTrace ?? "");
        }
    }

    static async Task LogErrorAsync(string source, string message, string detail)
    {
        try
        {
            var request = new LogErrorRequest(source, message, detail, null, null, null, null);
            await _errorRepository.LogErrorAsync(request);
        }
        catch
        {
            // Swallow errors in error logger to avoid infinite loops
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
