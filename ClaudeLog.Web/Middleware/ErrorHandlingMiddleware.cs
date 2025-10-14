using ClaudeLog.Data.Services;

namespace ClaudeLog.Web.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, LoggingService loggingService)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await loggingService.LogErrorAsync(
                "WebApi",
                ex.Message,
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
