using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ClaudeLog.Data;
using ClaudeLog.Data.Models;
using ClaudeLog.Data.Services;

namespace ClaudeLog.Hook.Gemini;

/// <summary>
/// Gemini CLI hook that captures conversation data and logs it to the ClaudeLog database.
/// This hook should be triggered by a Gemini CLI lifecycle event, like 'session-end'.
/// </summary>
class Program
{
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

            // Deserialize payload
            var hookInput = JsonSerializer.Deserialize<HookInput>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (hookInput?.SessionId != null && hookInput.Question != null && hookInput.Response != null)
            {
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", $"Processing entry for session {hookInput.SessionId}", LogLevel.Debug);
                await ProcessEntryAsync(hookInput);
            }
            else
            {
                await _diagnosticsService.WriteDiagnosticsAsync("Hook.Gemini", "Payload missing required fields (SessionId, Question, Response)", LogLevel.Warning, json);
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
            // Ensure session exists
            await _conversationService!.EnsureSessionAsync(input.SessionId!, "GeminiCLI");

            // Write the conversation entry
            var entryId = await _conversationService!.WriteEntryAsync(input.SessionId!, input.Question!, input.Response!);
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", $"Entry written successfully (ID: {entryId})", LogLevel.Info);
        }
        catch (Exception ex)
        {
            await _diagnosticsService!.WriteDiagnosticsAsync("Hook.Gemini", $"Failed to process entry: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
        }
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
}