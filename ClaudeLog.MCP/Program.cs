using ClaudeLog.Data;
using ClaudeLog.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var debugEnabled = (Environment.GetEnvironmentVariable("CLAUDELOG_DEBUG") ?? "0") == "1";
var waitForDebugger = (Environment.GetEnvironmentVariable("CLAUDELOG_WAIT_FOR_DEBUGGER") ?? "0") == "1";
var debuggerWaitSeconds = int.TryParse(
    Environment.GetEnvironmentVariable("CLAUDELOG_DEBUGGER_WAIT_SECONDS") ?? "60",
    out var seconds) ? seconds : 60;

// Wait for debugger attachment if requested
if (waitForDebugger)
{
    var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
    await Console.Error.WriteLineAsync($"[ClaudeLog.MCP] Waiting for debugger - PID: {pid}");

    for (int i = 0; i < debuggerWaitSeconds; i++)
    {
        if (System.Diagnostics.Debugger.IsAttached)
        {
            await Console.Error.WriteLineAsync($"[ClaudeLog.MCP] Debugger attached");
            System.Diagnostics.Debugger.Break();
            break;
        }
        await Task.Delay(1000);
    }

    if (!System.Diagnostics.Debugger.IsAttached)
    {
        await Console.Error.WriteLineAsync($"[ClaudeLog.MCP] Debugger wait timeout");
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Add MCP server with stdio transport (required for Codex integration)
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Add data layer services
builder.Services.AddSingleton<DbContext>();
builder.Services.AddSingleton<ConversationService>();
builder.Services.AddSingleton(sp =>
{
    var dbContext = sp.GetRequiredService<DbContext>();
    var diagnosticsService = new DiagnosticsService(dbContext);

    // Enable debug logging if CLAUDELOG_DEBUG=1
    if (debugEnabled)
    {
        diagnosticsService.DebugEnabled = true;
    }

    return diagnosticsService;
});

var app = builder.Build();

// Log MCP server start
var diagnosticsService = app.Services.GetRequiredService<DiagnosticsService>();
await diagnosticsService.WriteDiagnosticsAsync("MCP", "MCP server started", ClaudeLog.Data.Models.LogLevel.Debug);

await app.RunAsync();
