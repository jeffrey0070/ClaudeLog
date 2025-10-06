using ClaudeLog.Web.Api;
using ClaudeLog.Web.Data;
using ClaudeLog.Web.Middleware;
using ClaudeLog.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register data layer services
var connectionString = builder.Configuration.GetConnectionString("ClaudeLog");
builder.Services.AddSingleton(new ClaudeLog.Data.DbContext(connectionString));
builder.Services.AddScoped<ClaudeLog.Data.Repositories.SectionRepository>();
builder.Services.AddScoped<ClaudeLog.Data.Repositories.EntryRepository>();
builder.Services.AddScoped<ClaudeLog.Data.Repositories.ErrorRepository>();

// Register web services (legacy Db for backwards compatibility)
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<MarkdownRenderer>();
builder.Services.AddScoped<ErrorLogger>();

var app = builder.Build();

// Display startup info
var env = app.Environment;
var config = app.Configuration;
var urls = config["Kestrel:Endpoints:Http:Url"] ?? "????";

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

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// Map API endpoints
app.MapSectionsEndpoints();
app.MapEntriesEndpoints();
app.MapErrorsEndpoints();

app.Run();
