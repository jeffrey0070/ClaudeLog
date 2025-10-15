using System.ComponentModel;
using System.Text.Json;
using ClaudeLog.Data.Models;
using ClaudeLog.Data.Services;
using ModelContextProtocol.Server;

namespace ClaudeLog.MCP;

/// <summary>
/// MCP tools for writing conversations to ClaudeLog
/// </summary>
[McpServerToolType]
public static class LoggingTools
{
    /// <summary>
    /// Creates a new conversation session and returns its sessionId. Call this once at launch and reuse the id.
    /// </summary>
    /// <param name="tool">Logical tool/source name for this session (default: "Codex")</param>
    /// <param name="conversationService">Injected conversation service</param>
    /// <param name="diagnosticsService">Injected diagnostics service</param>
    /// <returns>JSON string: { success: bool, sessionId?: string, error?: string }</returns>
    [McpServerTool]
    [Description("Creates a new conversation session and returns its sessionId; call once and reuse.")]
    public static async Task<string> CreateSession(
        string? tool,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        var toolName = string.IsNullOrWhiteSpace(tool) ? "Codex" : tool!;

        try
        {
            var sessionId = await conversationService.CreateSessionAsync(toolName);
            return JsonSerializer.Serialize(new { success = true, sessionId });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("MCP.CreateSession", ex.Message, LogLevel.Error, ex.StackTrace ?? "");
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Writes a conversation entry (question and response pair) to ClaudeLog database.
    /// This tool should be called after completing a conversation turn to persist it for future reference.
    /// </summary>
    /// <param name="sessionId">Unique identifier for the conversation session (typically a GUID)</param>
    /// <param name="question">The user's question or prompt</param>
    /// <param name="response">The assistant's response</param>
    /// <param name="conversationService">Injected conversation service</param>
    /// <param name="diagnosticsService">Injected diagnostics service</param>
    /// <returns>Success status and entry ID if successful</returns>
    [McpServerTool]
    [Description("Writes a conversation entry (question and response) to ClaudeLog database. Call CreateSession first and reuse its sessionId for this tool.")]
    public static async Task<string> LogConversation(
        string sessionId,
        string question,
        string response,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return JsonSerializer.Serialize(new { success = false, error = "sessionId is required" });

        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(response))
            return JsonSerializer.Serialize(new { success = false, error = "Both question and response are required" });

        // Write the conversation entry (assumes session was created via CreateSession)
        try
        {
            var entryId = await conversationService.WriteEntryAsync(
                sessionId,
                question.Trim(),
                response.Trim());

            return JsonSerializer.Serialize(new { success = true, entryId });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("MCP.LogConversation", ex.Message, LogLevel.Error, ex.StackTrace ?? "", sessionId: sessionId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Gets information about the ClaudeLog MCP server
    /// </summary>
    [McpServerTool]
    [Description("Returns information about ClaudeLog MCP server capabilities and status")]
    public static string GetServerInfo()
    {
        return JsonSerializer.Serialize(new
        {
            name = "ClaudeLog MCP Server",
            version = "1.0.0",
            capabilities = new[] { "create_session", "log_conversation" }
        });
    }
}
