using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ClaudeLog.MCP;

/// <summary>
/// MCP tools for logging conversations to ClaudeLog
/// </summary>
[McpServerToolType]
public static class LoggingTools
{
    /// <summary>
    /// Creates a new logging section and returns its sectionId. Call this once at launch and reuse the id.
    /// </summary>
    /// <param name="tool">Logical tool/source name for this session (default: "Codex")</param>
    /// <param name="loggingService">Injected logging service</param>
    /// <returns>JSON string: { success: bool, sectionId?: string, error?: string }</returns>
    [McpServerTool]
    [Description("Creates a new logging section and returns its sectionId; call once and reuse.")]
    public static async Task<string> CreateSection(
        string? tool,
        LoggingService loggingService)
    {
        var toolName = string.IsNullOrWhiteSpace(tool) ? "Codex" : tool!;

        var (success, sectionId, error) = await loggingService.CreateSectionAsync(toolName);
        if (success && !string.IsNullOrWhiteSpace(sectionId))
        {
            return $"{{\"success\": true, \"sectionId\": \"{sectionId}\"}}";
        }

        var errorMsg = error ?? "Failed to create section";
        return $"{{\"success\": false, \"error\": \"{errorMsg.Replace("\"", "\\\"")}\"}}";
    }

    /// <summary>
    /// Logs a conversation (question and response pair) to ClaudeLog database.
    /// This tool should be called after completing a conversation turn to persist it for future reference.
    /// </summary>
    /// <param name="sessionId">Unique identifier for the conversation session (typically a GUID)</param>
    /// <param name="question">The user's question or prompt</param>
    /// <param name="response">The assistant's response</param>
    /// <param name="loggingService">Injected logging service</param>
    /// <returns>Success status and entry ID if successful</returns>
    [McpServerTool]
    [Description("Logs a conversation (question and response) to ClaudeLog database. Call CreateSection first and reuse its sectionId for this tool.")]
    public static async Task<string> LogConversation(
        string sessionId,
        string question,
        string response,
        LoggingService loggingService)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return "{\"success\": false, \"error\": \"sessionId is required\"}";

        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(response))
            return "{\"success\": false, \"error\": \"Both question and response are required\"}";

        // Log the conversation entry (assumes section was created via CreateSection)
        var (success, entryId) = await loggingService.LogEntryAsync(
            sessionId,
            question.Trim(),
            response.Trim());

        if (success && entryId.HasValue)
            return $"{{\"success\": true, \"entryId\": {entryId.Value}}}";

        return "{\"success\": false, \"error\": \"Failed to log entry\"}";
    }

    /// <summary>
    /// Gets information about the ClaudeLog MCP server
    /// </summary>
    [McpServerTool]
    [Description("Returns information about ClaudeLog MCP server capabilities and status")]
    public static string GetServerInfo()
    {
        return "{\"name\": \"ClaudeLog MCP Server\", \"version\": \"1.0.0\", \"capabilities\": [\"create_section\", \"log_conversation\"]}";
    }
}
