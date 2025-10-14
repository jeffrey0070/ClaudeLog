using ClaudeLog.Data.Services;
using ClaudeLog.Web.Api.Dtos;

namespace ClaudeLog.Web.Api;

public static class SessionsEndpoints
{
    public static void MapSessionsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sessions");

        group.MapPost("/", CreateSession);
        group.MapGet("/", GetSessions);
        group.MapPatch("/{sessionId}/deleted", UpdateSessionDeleted);
    }

    private static async Task<IResult> CreateSession(
        CreateSessionRequest request,
        LoggingService service)
    {
        try
        {
            // EnsureSessionAsync is idempotent - safe to call multiple times
            var success = await service.EnsureSessionAsync(
                request.SessionId ?? Guid.NewGuid().ToString(),
                request.Tool ?? "Unknown");

            if (success)
            {
                return Results.Ok(new CreateSessionResponse(request.SessionId ?? Guid.NewGuid().ToString()));
            }
            return Results.Problem("Failed to create session");
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetSessions(
        LoggingService service,
        int days = 30,
        int page = 1,
        int pageSize = 50,
        bool includeDeleted = false)
    {
        try
        {
            var sessions = await service.GetSessionsAsync(days, page, pageSize, includeDeleted);
            return Results.Ok(sessions);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateSessionDeleted(
        string sessionId,
        UpdateSessionDeletedRequest request,
        LoggingService service)
    {
        try
        {
            await service.UpdateSessionDeletedAsync(sessionId, request.IsDeleted);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
