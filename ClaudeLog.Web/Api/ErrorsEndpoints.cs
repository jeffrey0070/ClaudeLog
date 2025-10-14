using ClaudeLog.Data.Models;
using ClaudeLog.Data.Services;
using ClaudeLog.Web.Api.Dtos;

namespace ClaudeLog.Web.Api;

public static class ErrorsEndpoints
{
    public static void MapErrorsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/errors", LogError);
    }

    private static async Task<IResult> LogError(
        LogErrorRequest request,
        LoggingService service)
    {
        try
        {
            var id = await service.LogAsync(
                request.Source,
                request.Message,
                ClaudeLog.Data.Models.LogLevel.Error,
                request.Detail,
                request.Path,
                request.SessionId,
                request.EntryId,
                request.CreatedAt);

            if (id.HasValue)
            {
                return Results.Ok(new LogErrorResponse(true, id.Value));
            }
            return Results.Problem("Failed to log error");
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
