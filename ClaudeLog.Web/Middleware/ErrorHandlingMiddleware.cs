using ClaudeLog.Data.Services;
using LogLevel = ClaudeLog.Data.Models.LogLevel;

namespace ClaudeLog.Web.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DiagnosticsService diagnosticsService)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync(
                "WebApi",
                ex.Message,
                LogLevel.Error,
                ex.StackTrace ?? "",
                context.Request.Path
            );

            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "An internal error occurred",
                message = ex.Message
            });
        }
    }
}
