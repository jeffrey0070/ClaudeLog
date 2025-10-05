using ClaudeLog.Web.Api.Dtos;
using ClaudeLog.Web.Services;

namespace ClaudeLog.Web.Api;

public static class ErrorsEndpoints
{
    public static void MapErrorsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/errors", LogError);
    }

    private static async Task<IResult> LogError(
        LogErrorRequest request,
        ErrorLogger logger)
    {
        try
        {
            Guid? sectionId = null;
            if (!string.IsNullOrWhiteSpace(request.SectionId))
            {
                sectionId = Guid.Parse(request.SectionId);
            }

            var id = await logger.LogErrorAsync(
                request.Source,
                request.Message,
                request.Detail,
                request.Path,
                sectionId,
                request.EntryId
            );

            return Results.Ok(new LogErrorResponse(true, id ?? 0));
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
