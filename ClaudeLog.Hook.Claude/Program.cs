using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeLog.Hook.Claude;

class Program
{
    private const string ApiBaseUrl = "http://localhost:5089/api";
    private static readonly HttpClient httpClient = new();

    static async Task Main(string[] args)
    {
        try
        {
            // Read hook input from stdin
            using var reader = new StreamReader(Console.OpenStandardInput());
            var json = await reader.ReadToEndAsync();

            var hookInput = JsonSerializer.Deserialize<HookInput>(json);

            if (hookInput?.TranscriptPath != null && hookInput.SessionId != null)
            {
                await ProcessTranscriptAsync(hookInput.SessionId, hookInput.TranscriptPath);
            }

            // Output empty JSON (no modifications needed)
            Console.WriteLine("{}");
        }
        catch (Exception ex)
        {
            // Log error but don't fail the hook
            await LogErrorAsync("Hook.Claude", ex.Message, ex.StackTrace ?? "");
            Console.WriteLine("{}");
        }
    }

    static async Task ProcessTranscriptAsync(string sessionId, string transcriptPath)
    {
        // Expand environment variables in path
        transcriptPath = Environment.ExpandEnvironmentVariables(transcriptPath);

        if (!File.Exists(transcriptPath))
        {
            await LogErrorAsync("Hook.Claude", $"Transcript file not found: {transcriptPath}", "");
            return;
        }

        // Read last Q&A from transcript
        var (question, response) = await ReadLastInteractionAsync(transcriptPath);

        if (string.IsNullOrEmpty(question) || string.IsNullOrEmpty(response))
        {
            return; // Nothing to log
        }

        // Ensure section exists for this session
        await EnsureSectionAsync(sessionId);

        // Log the conversation entry
        await LogEntryAsync(sessionId, question, response);
    }

    static async Task<(string? question, string? response)> ReadLastInteractionAsync(string transcriptPath)
    {
        try
        {
            var lines = await File.ReadAllLinesAsync(transcriptPath);
            var messages = new List<TranscriptMessage>();

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
            var request = new
            {
                tool = "ClaudeCode",
                sectionId = sessionId
            };

            var response = await httpClient.PostAsJsonAsync($"{ApiBaseUrl}/sections", request);
            // Don't throw on error - section might already exist
        }
        catch
        {
            // Swallow errors - best effort
        }
    }

    static async Task LogEntryAsync(string sessionId, string question, string response)
    {
        try
        {
            var request = new
            {
                sectionId = sessionId,
                question,
                response
            };

            await httpClient.PostAsJsonAsync($"{ApiBaseUrl}/entries", request);
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
            var request = new
            {
                source,
                message,
                detail
            };

            await httpClient.PostAsJsonAsync($"{ApiBaseUrl}/errors", request);
        }
        catch
        {
            // Swallow errors in error logger
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
