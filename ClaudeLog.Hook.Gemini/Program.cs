using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ClaudeLog.Data;
using ClaudeLog.Data.Models;
using ClaudeLog.Data.Services;

namespace ClaudeLog.Hook.Gemini;

/// <summary>
/// Gemini CLI hook that captures conversation data and logs it to the ClaudeLog database.
/// This hook should be triggered by a Gemini CLI lifecycle event, like 'AfterModel'.
/// </summary>
class Program
{
    private static readonly string StateDir =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "/ClaudeLog";
    private static readonly string StatePath = Path.Combine(StateDir, "gemini_state.json");
    private static readonly string StreamStatePath = Path.Combine(StateDir, "gemini_stream_state.json");

    private static readonly bool _debugEnabled = (Environment.GetEnvironmentVariable("CLAUDELOG_DEBUG") ?? "0") == "1";
    private static readonly bool _waitForDebugger = (Environment.GetEnvironmentVariable("CLAUDELOG_WAIT_FOR_DEBUGGER") ?? "0") == "1";
    private static readonly int _debuggerWaitSeconds = int.TryParse(Environment.GetEnvironmentVariable("CLAUDELOG_DEBUGGER_WAIT_SECONDS") ?? "60", out var seconds) ? seconds : 60;
    private static readonly bool _dumpPayload = (Environment.GetEnvironmentVariable("CLAUDELOG_GEMINI_DUMP_PAYLOAD") ?? "0") == "1";

    private static ConversationService? _conversationService;
    private static DiagnosticsService? _diagnosticsService;

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Initialize services
            try
            {
                var dbContext = new DbContext();
                _conversationService = new ConversationService(dbContext);
                _diagnosticsService = new DiagnosticsService(dbContext);
                if (_debugEnabled)
                {
                    _diagnosticsService.DebugEnabled = true;
                }
            }
            catch (Exception ex)
            {
                await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Gemini] CRITICAL: Failed to initialize database services: {ex.Message}");
                return 1;
            }

            await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", "Hook started", LogLevel.Debug);

            Directory.CreateDirectory(StateDir);
            await CheckpointStore.LoadAsync(StatePath);
            await StreamStateStore.LoadAsync(StreamStatePath);

            // Optional: Wait for debugger to attach
            if (_waitForDebugger)
            {
                await AttachDebuggerAsync();
            }

            // Read payload from stdin
            using var reader = new StreamReader(Console.OpenStandardInput());
            var json = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(json))
            {
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", "Received empty payload from stdin.", LogLevel.Warning);
                Console.WriteLine("{}");
                return 0;
            }

            await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", "Received hook input", LogLevel.Trace, json);

            // Optional: Dump payload to a file for inspection
            if (_dumpPayload)
            {
                var dumpPath = Path.Combine(Path.GetTempPath(), "gemini-payload-dump.json");
                await File.WriteAllTextAsync(dumpPath, json);
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", $"Payload dumped to {dumpPath}", LogLevel.Debug);
            }

            // Deserialize payload with flexible extraction
            var hookInput = JsonSerializer.Deserialize<HookInput>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var extracted = ExtractHookInput(root, json, hookInput);

            if (extracted != null)
            {
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", $"Processing entry for session {extracted.SessionId}", LogLevel.Debug);
                await ProcessEntryAsync(extracted);
            }
            else
            {
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", "Payload missing required fields after extraction (Question, Response)", LogLevel.Warning, json);
            }

            // Output empty JSON as the hook doesn't modify anything
            Console.WriteLine("{}");
            await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", "Hook completed successfully", LogLevel.Debug);
            return 0;
        }
        catch (Exception ex)
        {
            if (_diagnosticsService != null)
            {
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", "Unhandled exception in Main", LogLevel.Critical, $"{ex.Message}\n{ex.StackTrace}");
            }
            await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Gemini] CRITICAL: {ex.Message}");
            Console.WriteLine("{}");
            return 1;
        }
        finally
        {
            await CheckpointStore.SaveAsync(StatePath);
            await StreamStateStore.SaveAsync(StreamStatePath);
        }
    }

    /// <summary>
    /// Pauses execution to allow a debugger to attach.
    /// </summary>
    static async Task AttachDebuggerAsync()
    {
        var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
        var message = $"Waiting for debugger to attach. PID: {pid}. Waiting for {_debuggerWaitSeconds} seconds.";
        await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", message, LogLevel.Debug);
        await Console.Error.WriteLineAsync($"[ClaudeLog.Hook.Gemini] {message}");

        for (int i = 0; i < _debuggerWaitSeconds; i++)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", "Debugger attached.", LogLevel.Debug);
                System.Diagnostics.Debugger.Break();
                return;
            }
            await Task.Delay(1000);
        }

        await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", "Debugger wait timeout.", LogLevel.Warning);
    }

    /// <summary>
    /// Processes the deserialized hook input and logs it to the database.
    /// </summary>
    static async Task ProcessEntryAsync(HookInput input)
    {
        try
        {
            var (question, response, transcriptSize) = await ResolveQuestionResponseAsync(input);
            if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(response))
            {
                if (string.Equals(input.HookEventName, "AfterModel", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", "Failed to resolve Q&A from payload", LogLevel.Warning);
                return;
            }

            var checkpointKey = input.TranscriptPath ?? input.SessionId!;
            var hash = HashPair(question, response);
            if (CheckpointStore.IsDuplicate(checkpointKey, hash))
            {
                await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", "Duplicate entry skipped", LogLevel.Debug);
                return;
            }

            // Ensure session exists
            await _conversationService!.EnsureSessionAsync(input.SessionId!, "GeminiCLI");

            // Write the conversation entry
            var entryId = await _conversationService!.WriteEntryAsync(input.SessionId!, question, response);
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", $"Entry written successfully (ID: {entryId})", LogLevel.Info);

            CheckpointStore.Update(checkpointKey, new CheckpointRecord
            {
                LastHash = hash,
                LastEntryAt = DateTime.Now,
                LastSize = transcriptSize ?? 0
            });
        }
        catch (Exception ex)
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", $"Failed to process entry: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
        }
    }

    static HookInput? ExtractHookInput(JsonElement root, string json, HookInput? directInput)
    {
        string? sessionId = directInput?.SessionId;
        string? question = directInput?.Question;
        string? response = directInput?.Response;
        var transcriptPath = directInput?.TranscriptPath;
        var hookEventName = directInput?.HookEventName;

        sessionId = NormalizeMaybeEmpty(sessionId) ?? ExtractSessionId(root);
        transcriptPath = NormalizeMaybeEmpty(transcriptPath) ?? ExtractTranscriptPath(root);
        hookEventName = NormalizeMaybeEmpty(hookEventName) ?? ExtractHookEventName(root);

        if (string.Equals(hookEventName, "AfterModel", StringComparison.OrdinalIgnoreCase))
        {
            question = NormalizeMaybeEmpty(question) ?? ExtractLastUserMessageFromRequest(root);
            var chunkText = ExtractAfterModelChunkText(root);
            var isFinal = IsAfterModelFinal(root);
            response = NormalizeMaybeEmpty(response) ?? chunkText;

            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                return new HookInput
                {
                    SessionId = sessionId.Trim(),
                    Question = question?.Trim(),
                    Response = response?.Trim(),
                    TranscriptPath = transcriptPath,
                    HookEventName = hookEventName,
                    AfterModelChunkText = chunkText,
                    AfterModelIsFinal = isFinal
                };
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(response))
            {
                if (TryExtractQuestionResponse(root, out var extractedQuestion, out var extractedResponse))
                {
                    question = NormalizeMaybeEmpty(question) ?? extractedQuestion;
                    response = NormalizeMaybeEmpty(response) ?? extractedResponse;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = DeterministicGuidFromString(json).ToString();
        }

        if (!string.IsNullOrWhiteSpace(sessionId) &&
            !string.IsNullOrWhiteSpace(question) &&
            !string.IsNullOrWhiteSpace(response))
        {
            return new HookInput
            {
                SessionId = sessionId.Trim(),
                Question = question.Trim(),
                Response = response.Trim(),
                TranscriptPath = transcriptPath,
                HookEventName = hookEventName
            };
        }

        return null;
    }

    static string? NormalizeMaybeEmpty(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }

    static string? ExtractSessionId(JsonElement root)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "session_id",
            "sessionId",
            "session",
            "conversation_id",
            "conversationId",
            "thread_id",
            "threadId",
            "chat_id",
            "chatId"
        };

        var direct = FindStringByKeys(root, keys);
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        // Common nested path: session.id
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("session", out var sessionObj) &&
            sessionObj.ValueKind == JsonValueKind.Object)
        {
            var nested = FindStringByKeys(sessionObj, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "session_id", "sessionId" });
            if (!string.IsNullOrWhiteSpace(nested)) return nested;
        }

        return null;
    }

    static string? ExtractTranscriptPath(JsonElement root)
    {
        return ExtractStringProperty(root, new[] { "transcript_path", "transcriptPath" });
    }

    static string? ExtractHookEventName(JsonElement root)
    {
        return ExtractStringProperty(root, new[] { "hook_event_name", "hookEventName" });
    }

    static async Task<(string? Question, string? Response, long? TranscriptSize)> ResolveQuestionResponseAsync(HookInput input)
    {
        if (string.Equals(input.HookEventName, "AfterModel", StringComparison.OrdinalIgnoreCase))
        {
            var resolved = StreamStateStore.AppendChunk(
                input.SessionId ?? string.Empty,
                input.Question,
                input.AfterModelChunkText,
                input.AfterModelIsFinal);

            if (resolved.HasValue)
            {
                return (resolved.Value.Question, resolved.Value.Response, null);
            }

            return (null, null, null);
        }

        if (!string.IsNullOrWhiteSpace(input.TranscriptPath))
        {
            var pair = await ExtractLastPairFromTranscriptAsync(input.TranscriptPath);
            if (pair.HasValue)
            {
                return (pair.Value.Question, pair.Value.Response, pair.Value.Size);
            }
        }

        return (input.Question, input.Response, null);
    }

    static async Task<(string Question, string Response, long Size)?> ExtractLastPairFromTranscriptAsync(string transcriptPath)
    {
        var expandedPath = Environment.ExpandEnvironmentVariables(transcriptPath);
        if (!File.Exists(expandedPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(expandedPath);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!doc.RootElement.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? lastUser = null;
            string? lastAssistant = null;

            foreach (var item in messages.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var type = ExtractStringProperty(item, new[] { "type", "role" });
                var content = ExtractStringProperty(item, new[] { "content", "text", "message" });
                if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                type = type.Trim().ToLowerInvariant();
                if (type == "user")
                {
                    lastUser = content;
                }
                else if (type == "gemini" || type == "assistant" || type == "model")
                {
                    if (!string.IsNullOrWhiteSpace(lastUser))
                    {
                        lastAssistant = content;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(lastUser) && !string.IsNullOrWhiteSpace(lastAssistant))
            {
                var size = new FileInfo(expandedPath).Length;
                return (lastUser.Trim(), lastAssistant.Trim(), size);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    static bool TryExtractQuestionResponse(JsonElement root, out string question, out string response)
    {
        question = string.Empty;
        response = string.Empty;

        var questionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "question",
            "prompt",
            "input",
            "input_text",
            "user_input",
            "query",
            "user"
        };

        var responseKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "response",
            "answer",
            "output",
            "assistant",
            "model",
            "model_response",
            "completion",
            "llm_response",
            "llmResponse"
        };

        var directQuestion = FindStringByKeys(root, questionKeys);
        var directResponse = FindStringByKeys(root, responseKeys);
        if (!string.IsNullOrWhiteSpace(directQuestion) && !string.IsNullOrWhiteSpace(directResponse))
        {
            question = directQuestion;
            response = directResponse;
            return true;
        }

        if (TryExtractFromMessageArrays(root, out var messageQuestion, out var messageResponse))
        {
            question = messageQuestion;
            response = messageResponse;
            return true;
        }

        if (TryExtractFromLlmRequestResponse(root, out var llmQuestion, out var llmResponse))
        {
            question = llmQuestion;
            response = llmResponse;
            return true;
        }

        return false;
    }

    static bool TryExtractFromMessageArrays(JsonElement root, out string question, out string response)
    {
        question = string.Empty;
        response = string.Empty;

        var messageArrayKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "messages",
            "turns",
            "history",
            "conversation",
            "chat",
            "entries"
        };

        foreach (var array in FindArraysByName(root, messageArrayKeys))
        {
            if (TryExtractFromMessageArray(array, out question, out response))
            {
                return true;
            }
        }

        return false;
    }

    static bool TryExtractFromLlmRequestResponse(JsonElement root, out string question, out string response)
    {
        question = string.Empty;
        response = string.Empty;

        if (!root.TryGetProperty("llm_request", out var llmRequest) || llmRequest.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!root.TryGetProperty("llm_response", out var llmResponse))
        {
            return false;
        }

        if (!llmRequest.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var lastUser = ExtractLastUserMessage(messages);
        var modelResponse = ExtractLlmResponseText(llmResponse);

        if (string.IsNullOrWhiteSpace(lastUser) || string.IsNullOrWhiteSpace(modelResponse))
        {
            return false;
        }

        question = lastUser;
        response = modelResponse;
        return true;
    }

    static string? ExtractLastUserMessage(JsonElement messages)
    {
        string? lastUser = null;
        foreach (var item in messages.EnumerateArray())
        {
            var role = ExtractMessageRole(item);
            if (role != MessageRole.User) continue;

            var text = ExtractMessageText(item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                lastUser = text;
            }
        }
        return lastUser;
    }

    static string? ExtractLastUserMessageFromRequest(JsonElement root)
    {
        if (!root.TryGetProperty("llm_request", out var llmRequest) || llmRequest.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        if (!llmRequest.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        return ExtractLastUserMessage(messages);
    }

    static string? ExtractAfterModelChunkText(JsonElement root)
    {
        if (!root.TryGetProperty("llm_response", out var response))
        {
            return null;
        }
        return ExtractLlmResponseText(response);
    }

    static bool IsAfterModelFinal(JsonElement root)
    {
        if (!root.TryGetProperty("llm_response", out var response))
        {
            return false;
        }

        if (response.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in candidates.EnumerateArray())
            {
                if (candidate.TryGetProperty("finishReason", out var finishEl) &&
                    finishEl.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(finishEl.GetString()))
                {
                    return true;
                }
            }
        }
        return false;
    }

    static string? ExtractLlmResponseText(JsonElement llmResponse)
    {
        if (llmResponse.ValueKind == JsonValueKind.Object)
        {
            if (llmResponse.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            {
                return textEl.GetString();
            }

            if (llmResponse.TryGetProperty("candidate", out var candidate))
            {
                var candidateText = ExtractCandidateText(candidate);
                if (!string.IsNullOrWhiteSpace(candidateText)) return candidateText;
            }

            if (llmResponse.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in candidates.EnumerateArray())
                {
                    var candidateText = ExtractCandidateText(item);
                    if (!string.IsNullOrWhiteSpace(candidateText))
                    {
                        return candidateText;
                    }
                }
            }
        }

        return ExtractTextFromElement(llmResponse);
    }

    static string? ExtractCandidateText(JsonElement candidate)
    {
        if (candidate.ValueKind != JsonValueKind.Object)
        {
            return ExtractTextFromElement(candidate);
        }

        if (candidate.TryGetProperty("content", out var content))
        {
            var text = ExtractTextFromElement(content);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        if (candidate.TryGetProperty("text", out var textEl))
        {
            var text = ExtractTextFromElement(textEl);
            if (!string.IsNullOrWhiteSpace(text)) return text;
        }

        return ExtractTextFromElement(candidate);
    }

    static IEnumerable<JsonElement> FindArraysByName(JsonElement element, HashSet<string> names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array && names.Contains(property.Name))
                {
                    yield return property.Value;
                }

                foreach (var child in FindArraysByName(property.Value, names))
                {
                    yield return child;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                foreach (var child in FindArraysByName(item, names))
                {
                    yield return child;
                }
            }
        }
    }

    static bool TryExtractFromMessageArray(JsonElement array, out string question, out string response)
    {
        question = string.Empty;
        response = string.Empty;

        string? lastUser = null;
        string? lastAssistant = null;
        var fallbackMessages = new List<string>();

        foreach (var item in array.EnumerateArray())
        {
            var text = ExtractMessageText(item);
            if (string.IsNullOrWhiteSpace(text)) continue;

            var role = ExtractMessageRole(item);
            if (role == MessageRole.User)
            {
                lastUser = text;
            }
            else if (role == MessageRole.Assistant)
            {
                lastAssistant = text;
            }
            else
            {
                fallbackMessages.Add(text);
            }
        }

        if (!string.IsNullOrWhiteSpace(lastUser) && !string.IsNullOrWhiteSpace(lastAssistant))
        {
            question = lastUser;
            response = lastAssistant;
            return true;
        }

        if (fallbackMessages.Count >= 2)
        {
            question = fallbackMessages[^2];
            response = fallbackMessages[^1];
            return true;
        }

        return false;
    }

    enum MessageRole
    {
        Unknown,
        User,
        Assistant
    }

    static MessageRole ExtractMessageRole(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return MessageRole.Unknown;
        }

        var role = ExtractStringProperty(item, new[] { "role", "author", "sender", "participant", "type" });
        if (string.IsNullOrWhiteSpace(role))
        {
            return MessageRole.Unknown;
        }

        role = role.Trim().ToLowerInvariant();
        if (role.Contains("user") || role.Contains("human"))
        {
            return MessageRole.User;
        }

        if (role.Contains("assistant") || role.Contains("model") || role.Contains("bot"))
        {
            return MessageRole.Assistant;
        }

        return MessageRole.Unknown;
    }

    static string? ExtractMessageText(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String)
        {
            return item.GetString();
        }

        if (item.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var direct = ExtractStringProperty(item, new[] { "text", "content", "message", "value" });
        if (!string.IsNullOrWhiteSpace(direct))
        {
            return direct;
        }

        if (item.TryGetProperty("content", out var content))
        {
            var extracted = ExtractTextFromElement(content);
            if (!string.IsNullOrWhiteSpace(extracted)) return extracted;
        }

        if (item.TryGetProperty("parts", out var parts))
        {
            var extracted = ExtractTextFromElement(parts);
            if (!string.IsNullOrWhiteSpace(extracted)) return extracted;
        }

        return null;
    }

    static string? ExtractStringProperty(JsonElement element, string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value))
            {
                var text = ExtractTextFromElement(value);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        return null;
    }

    static string? ExtractTextFromElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                var text = ExtractTextFromElement(item);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    parts.Add(text);
                }
            }
            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            {
                return textEl.GetString();
            }

            if (element.TryGetProperty("parts", out var partsEl))
            {
                var combined = ExtractTextFromElement(partsEl);
                if (!string.IsNullOrWhiteSpace(combined)) return combined;
            }

            if (element.TryGetProperty("content", out var contentEl))
            {
                var combined = ExtractTextFromElement(contentEl);
                if (!string.IsNullOrWhiteSpace(combined)) return combined;
            }
        }

        return null;
    }

    static string? FindStringByKeys(JsonElement element, HashSet<string> keys)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (keys.Contains(property.Name))
                {
                    var text = ExtractTextFromElement(property.Value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }

                var nested = FindStringByKeys(property.Value, keys);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindStringByKeys(item, keys);
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    return nested;
                }
            }
        }

        return null;
    }

    static Guid DeterministicGuidFromString(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }

    static string HashPair(string q, string r)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(q + "\n" + r));
        return Convert.ToHexString(bytes);
    }
}

/// <summary>
/// Represents the expected JSON payload from the Gemini CLI hook.
/// Based on common patterns from other hooks. This may need adjustment.
/// </summary>
class HookInput
{
    [JsonPropertyName("session_id")]
    public string? SessionId { get; set; }

    [JsonPropertyName("question")]
    public string? Question { get; set; }

    [JsonPropertyName("response")]
    public string? Response { get; set; }

    [JsonPropertyName("transcript_path")]
    public string? TranscriptPath { get; set; }

    [JsonPropertyName("hook_event_name")]
    public string? HookEventName { get; set; }

    [JsonIgnore]
    public string? AfterModelChunkText { get; set; }

    [JsonIgnore]
    public bool AfterModelIsFinal { get; set; }
}

class CheckpointRecord
{
    public long LastSize { get; set; }
    public string LastHash { get; set; } = "";
    public DateTime LastEntryAt { get; set; }
}

static class CheckpointStore
{
    private static readonly object Gate = new();
    private static Dictionary<string, CheckpointRecord> _map = new(StringComparer.OrdinalIgnoreCase);

    public static bool IsDuplicate(string key, string hash)
    {
        lock (Gate)
        {
            if (_map.TryGetValue(key, out var rec))
            {
                return string.Equals(rec.LastHash, hash, StringComparison.Ordinal);
            }
            return false;
        }
    }

    public static void Update(string key, CheckpointRecord rec)
    {
        lock (Gate) { _map[key] = rec; }
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

class StreamState
{
    public string Question { get; set; } = "";
    public string Response { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}

static class StreamStateStore
{
    private static readonly object Gate = new();
    private static Dictionary<string, StreamState> _map = new(StringComparer.OrdinalIgnoreCase);

    public static (string Question, string Response)? AppendChunk(string sessionId, string? question, string? chunkText, bool isFinal)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        lock (Gate)
        {
            if (!_map.TryGetValue(sessionId, out var state) ||
                (!string.IsNullOrWhiteSpace(question) && !string.Equals(state.Question, question, StringComparison.Ordinal)))
            {
                state = new StreamState
                {
                    Question = question ?? "",
                    Response = ""
                };
            }

            if (!string.IsNullOrWhiteSpace(question))
            {
                state.Question = question;
            }

            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                state.Response += chunkText;
            }
            state.UpdatedAt = DateTime.Now;
            _map[sessionId] = state;

            if (isFinal)
            {
                _map.Remove(sessionId);
                if (!string.IsNullOrWhiteSpace(state.Question) && !string.IsNullOrWhiteSpace(state.Response))
                {
                    return (state.Question, state.Response.Trim());
                }
                return null;
            }
        }

        return null;
    }

    public static async Task LoadAsync(string statePath)
    {
        try
        {
            if (!File.Exists(statePath)) return;
            var json = await File.ReadAllTextAsync(statePath);
            var tmp = JsonSerializer.Deserialize<Dictionary<string, StreamState>>(json);
            if (tmp != null)
            {
                lock (Gate) _map = new Dictionary<string, StreamState>(tmp, StringComparer.OrdinalIgnoreCase);
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
