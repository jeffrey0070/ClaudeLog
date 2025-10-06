using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeLog.Hook.Codex;

// Parses Codex/Claude-like transcripts and extracts the last user→assistant pair.
// Supports Codex JSONL lines with { type:'response_item', payload:{ role:'user|assistant', content:[{type:'input_text',text:'...'}] } }
// Also supports Claude-like { type:'user|assistant', message:{ content:[{type:'text',text:'...'}] } } and legacy role/content.
static class TranscriptParser
{
    public static async Task<Pair?> ExtractLastPairAsync(string transcriptPath)
    {
        try
        {
            // Read all lines (MVP); can optimize to incremental if needed
            var lines = await File.ReadAllLinesAsync(transcriptPath);
            if (lines.Length == 0) return null;

            var messages = new List<object>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var doc = JsonDocument.Parse(line);
                    messages.Add(doc.RootElement.Clone());
                }
                catch
                {
                    // Skip malformed/partial lines (e.g., file mid-write). We'll try an array fallback later if needed.
                    continue;
                }
            }

            // Fallback: some exports are a single JSON array instead of JSONL
            if (messages.Count == 0)
            {
                try
                {
                    var arrDoc = JsonDocument.Parse(await File.ReadAllTextAsync(transcriptPath));
                    if (arrDoc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var el in arrDoc.RootElement.EnumerateArray())
                            messages.Add(el.Clone());
                    }
                }
                catch { }
            }

            if (messages.Count == 0) return null;

            string? question = null;
            string? response = null;

            for (int i = messages.Count - 1; i >= 0; i--)
            {
                if (response == null)
                {
                    var text = TryExtractAssistant(messages[i]);
                    if (!string.IsNullOrWhiteSpace(text)) response = text;
                }
                else if (question == null)
                {
                    var text = TryExtractUser(messages[i]);
                    if (!string.IsNullOrWhiteSpace(text)) question = text;
                }

                if (question != null && response != null) break;
            }

            // If there's an assistant response but no captured user question, keep the entry with a placeholder.
            if (response != null && question == null)
            {
                question = "[missing user message]";
            }

            if (question == null || response == null) return null;
            return new Pair(question, response);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractAssistant(object msg)
    {
        if (msg is JsonElement el)
        {
            // Codex-like { type: "response_item", payload: { type:"message", role:"assistant", content:[...] } }
            if (el.TryGetProperty("payload", out var payload))
            {
                if (payload.TryGetProperty("role", out var role1) && role1.GetString() == "assistant")
                {
                    if (payload.TryGetProperty("content", out var contentEl))
                    {
                        var content = ExtractTextFromContent(contentEl);
                        if (!string.IsNullOrWhiteSpace(content)) return content;
                    }
                }
            }
            // Claude-like { type: "assistant", message: { content: [...] } }
            if (el.TryGetProperty("type", out var type) && type.GetString() == "assistant")
            {
                if (el.TryGetProperty("message", out var message))
                {
                    var content = ExtractTextFromContent(message);
                    if (!string.IsNullOrWhiteSpace(content)) return content;
                }
            }

            // Legacy { role: "assistant", content: ... }
            if (el.TryGetProperty("role", out var role2) && role2.GetString() == "assistant")
            {
                if (el.TryGetProperty("content", out var contentEl))
                {
                    var content = ExtractTextFromContent(contentEl);
                    if (!string.IsNullOrWhiteSpace(content)) return content;
                }
            }
        }
        return null;
    }

    private static string? TryExtractUser(object msg)
    {
        if (msg is JsonElement el)
        {
            // Codex event stream sometimes emits user messages as event_msg: { payload: { type: "user_message", message: "..." } }
            if (el.TryGetProperty("type", out var evtType) && evtType.GetString() == "event_msg")
            {
                if (el.TryGetProperty("payload", out var evtPayload)
                    && evtPayload.TryGetProperty("type", out var evtKind)
                    && evtKind.GetString() == "user_message")
                {
                    if (evtPayload.TryGetProperty("message", out var msgText) && msgText.ValueKind == JsonValueKind.String)
                        return msgText.GetString();
                }
            }

            if (el.TryGetProperty("payload", out var payload))
            {
                if (payload.TryGetProperty("role", out var role1) && role1.GetString() == "user")
                {
                    if (payload.TryGetProperty("content", out var contentEl))
                    {
                        var content = ExtractTextFromContent(contentEl);
                        if (!string.IsNullOrWhiteSpace(content)) return content;
                    }
                }
            }
            if (el.TryGetProperty("type", out var type) && type.GetString() == "user")
            {
                if (el.TryGetProperty("message", out var message))
                {
                    var content = ExtractTextFromContent(message);
                    if (!string.IsNullOrWhiteSpace(content)) return content;
                }
            }

            if (el.TryGetProperty("role", out var role2) && role2.GetString() == "user")
            {
                if (el.TryGetProperty("content", out var contentEl))
                {
                    var content = ExtractTextFromContent(contentEl);
                    if (!string.IsNullOrWhiteSpace(content)) return content;
                }
            }
        }
        return null;
    }

    private static string? ExtractTextFromContent(JsonElement contentRoot)
    {
        // If content itself is a string
        if (contentRoot.ValueKind == JsonValueKind.String)
            return contentRoot.GetString();

        // If content is an object: may directly have text, or nested content
        if (contentRoot.ValueKind == JsonValueKind.Object)
        {
            // Direct text property (covers { text: "..." } and { type: "input_text"|"output_text", text: "..." })
            if (contentRoot.TryGetProperty("text", out var objText))
                return objText.GetString();

            // Typed text with a text field
            if (contentRoot.TryGetProperty("type", out var objType))
            {
                var t = objType.GetString();
                if (t == "text" || t == "input_text" || t == "output_text")
                {
                    if (contentRoot.TryGetProperty("text", out var typedText))
                        return typedText.GetString();
                }
            }

            // Nested content
            if (contentRoot.TryGetProperty("content", out var inner))
                return ExtractTextFromContent(inner);
        }

        // If content is an array of blocks
        if (contentRoot.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var block in contentRoot.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.String)
                {
                    parts.Add(block.GetString() ?? "");
                    continue;
                }
                if (block.ValueKind == JsonValueKind.Object)
                {
                    // Check for "text" field first (works for all types)
                    if (block.TryGetProperty("text", out var tx))
                    {
                        parts.Add(tx.GetString() ?? "");
                    }
                    // Fallback: check for specific type field
                    else if (block.TryGetProperty("type", out var t))
                    {
                        var typeStr = t.GetString();
                        if (typeStr == "text" || typeStr == "input_text" || typeStr == "output_text")
                        {
                            if (block.TryGetProperty("text", out var tx2))
                                parts.Add(tx2.GetString() ?? "");
                        }
                    }
                }
            }
            return parts.Count > 0 ? string.Join("\n", parts) : null;
        }
        return null;
    }
}
