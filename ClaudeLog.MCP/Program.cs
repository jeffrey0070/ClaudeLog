using ClaudeLog.Data;
using ClaudeLog.Data.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

// Add MCP server with stdio transport (required for Codex integration)
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Add data layer services
builder.Services.AddSingleton<DbContext>();
builder.Services.AddSingleton<LoggingService>();

var app = builder.Build();
await app.RunAsync();
