using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeLog.Hook.Codex.Models;

class TranscriptMessage
{
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("message")] public MessageContent? Message { get; set; }
}

class MessageContent
{
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("content")] public JsonElement? Content { get; set; }
}

