using System;
using ClaudeLog.Web.Api;
using ClaudeLog.Web.Middleware;
using ClaudeLog.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;
var startupLogPath = Path.Combine(AppContext.BaseDirectory, "startup.log");
var urls = config["Kestrel:Endpoints:Http:Url"]
    ?? config["urls"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? (builder.Environment.IsDevelopment() ? "http://localhost:15089" : "http://localhost:15088");

builder.WebHost.UseUrls(urls);

void LogStartup(string message)
{
    var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
    Console.WriteLine(line);

    try
    {
        File.AppendAllText(startupLogPath, line + Environment.NewLine);
    }
    catch
    {
        // Startup logging should never block application startup.
    }
}

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Read connection string from environment (single source of truth)
var connectionString = Environment.GetEnvironmentVariable("CLAUDELOG_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connectionString))
{
    LogStartup("ERROR: CLAUDELOG_CONNECTION_STRING environment variable is not set.");
    LogStartup("Please run set-connection-string.bat or configure the variable manually.");
    return;
}

// Register data layer services
builder.Services.AddSingleton(new ClaudeLog.Data.DbContext());
builder.Services.AddScoped<ClaudeLog.Data.Services.ConversationService>();
builder.Services.AddScoped<ClaudeLog.Data.Services.DiagnosticsService>();

// Register web services
builder.Services.AddSingleton<MarkdownRenderer>();

var app = builder.Build();

// Initialize database (automatic creation and migrations)
var dbInitializer = new ClaudeLog.Data.DatabaseInitializer(connectionString!);
var maxWaitMinutes = 10;
if (int.TryParse(Environment.GetEnvironmentVariable("CLAUDELOG_DB_STARTUP_MAX_WAIT_MINUTES"), out var configuredMaxWaitMinutes)
    && configuredMaxWaitMinutes > 0)
{
    maxWaitMinutes = configuredMaxWaitMinutes;
}

var retryDelaySeconds = 10;
if (int.TryParse(Environment.GetEnvironmentVariable("CLAUDELOG_DB_STARTUP_RETRY_SECONDS"), out var configuredRetryDelaySeconds)
    && configuredRetryDelaySeconds > 0)
{
    retryDelaySeconds = configuredRetryDelaySeconds;
}

var startupDeadline = DateTime.UtcNow.AddMinutes(maxWaitMinutes);
var attempt = 0;
var dbInitialized = false;

while (!dbInitialized)
{
    attempt++;
    LogStartup($"Initializing database. Attempt {attempt}. Startup log: {startupLogPath}");
    dbInitialized = await dbInitializer.InitializeAsync();

    if (dbInitialized)
    {
        break;
    }

    if (DateTime.UtcNow >= startupDeadline)
    {
        break;
    }

    LogStartup($"Database initialization failed on attempt {attempt}. Retrying in {retryDelaySeconds} seconds.");
    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
}

if (!dbInitialized)
{
    LogStartup("Database initialization failed. Application cannot start.");
    LogStartup($"Retry window exhausted after {attempt} attempt(s) over up to {maxWaitMinutes} minute(s).");
    return;
}

// Display startup info
var env = app.Environment;

LogStartup("========================================");
LogStartup("ClaudeLog - Conversation Logger");
LogStartup("========================================");
LogStartup($"Environment: {env.EnvironmentName}");
LogStartup($"Listening on: {urls}");
LogStartup($"Database: {connectionString}");
LogStartup("========================================");
LogStartup("Press Ctrl+C to shut down");
LogStartup(string.Empty);

// Handle Ctrl+C gracefully
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    LogStartup("Shutting down...");
    lifetime.StopApplication();
};

// Use custom error handling middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// Only redirect to HTTPS in Development; disabled in Production.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// Map API endpoints
app.MapSessionsEndpoints();
app.MapEntriesEndpoints();
app.MapErrorsEndpoints();

app.Run();
