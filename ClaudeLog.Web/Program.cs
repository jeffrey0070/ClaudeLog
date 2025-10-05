using ClaudeLog.Web.Api;
using ClaudeLog.Web.Data;
using ClaudeLog.Web.Middleware;
using ClaudeLog.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

// Register custom services
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<MarkdownRenderer>();
builder.Services.AddScoped<ErrorLogger>();

var app = builder.Build();

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
