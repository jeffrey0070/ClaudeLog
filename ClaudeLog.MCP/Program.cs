using ClaudeLog.MCP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;

var builder = Host.CreateApplicationBuilder(args);

// Add MCP server with stdio transport (required for Codex integration)
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// Add logging service (using direct HttpClient to avoid factory issues with stdio transport)
builder.Services.AddSingleton<LoggingService>();

var app = builder.Build();
await app.RunAsync();
