using ClaudeLog.Data.Services;
using ClaudeLog.Web.Api.Dtos;
using LogLevel = ClaudeLog.Data.Models.LogLevel;

namespace ClaudeLog.Web.Api;

public static class ErrorsEndpoints
{
    public static void MapErrorsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/errors", LogError);
        app.MapGet("/api/errors", GetErrors);
    }

    private static async Task<IResult> LogError(
        LogErrorRequest request,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            var id = await diagnosticsService.WriteDiagnosticsAsync(
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
            return Results.Problem("Failed to write diagnostics");
        }
        catch (Exception ex)
        {
            // Try to write diagnostics for the failure of the error logging endpoint itself
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.LogError", $"Failed to write diagnostics from {request.Source}: {ex.Message}", LogLevel.Error, ex.StackTrace ?? "");
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetErrors(
        DiagnosticsService diagnosticsService,
        LogLevel? minLevel,
        string? source,
        int page = 1,
        int pageSize = 100)
    {
        var items = await diagnosticsService.GetLogsAsync(minLevel, source, page, pageSize);
        var dtos = items.Select(x => new ErrorLogDto(
            x.Id, x.Source, x.Message, x.Detail, x.Path, x.SessionId, x.EntryId, x.CreatedAt, x.LogLevel));
        return Results.Ok(dtos);
    }
}
