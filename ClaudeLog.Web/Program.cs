using ClaudeLog.Web.Api;
using ClaudeLog.Web.Middleware;
using ClaudeLog.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

// Register data layer services
var connectionString = builder.Configuration.GetConnectionString("ClaudeLog");
builder.Services.AddSingleton(new ClaudeLog.Data.DbContext(connectionString));
builder.Services.AddScoped<ClaudeLog.Data.Services.ConversationService>();
builder.Services.AddScoped<ClaudeLog.Data.Services.DiagnosticsService>();

// Register web services
builder.Services.AddSingleton<MarkdownRenderer>();

var app = builder.Build();

// Initialize database (automatic creation and migrations)
var dbInitializer = new ClaudeLog.Data.DatabaseInitializer(connectionString!);
var dbInitialized = await dbInitializer.InitializeAsync();

if (!dbInitialized)
{
    Console.WriteLine("\nDatabase initialization failed. Application cannot start.");
    Console.WriteLine("Please fix the connection string and try again.");
    return;
}

// Display startup info
var env = app.Environment;
var config = app.Configuration;
var urls = config["Kestrel:Endpoints:Http:Url"]
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? (app.Environment.IsDevelopment() ? "http://localhost:15089" : "http://localhost:15088");

Console.WriteLine("========================================");
Console.WriteLine("ClaudeLog - Conversation Logger");
Console.WriteLine("========================================");
Console.WriteLine($"Environment: {env.EnvironmentName}");
Console.WriteLine($"Listening on: {urls}");
Console.WriteLine($"Database: {config.GetConnectionString("ClaudeLog")}");
Console.WriteLine("========================================");
Console.WriteLine("Press Ctrl+C to shut down");
Console.WriteLine();

// Handle Ctrl+C gracefully
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\nShutting down...");
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
