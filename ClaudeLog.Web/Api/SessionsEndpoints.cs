using ClaudeLog.Data.Services;
using ClaudeLog.Web.Api.Dtos;
using LogLevel = ClaudeLog.Data.Models.LogLevel;

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
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            // EnsureSessionAsync is idempotent - safe to call multiple times
            var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
            await conversationService.EnsureSessionAsync(sessionId, request.Tool ?? "Unknown");
            return Results.Ok(new CreateSessionResponse(sessionId));
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.CreateSession", ex.Message, LogLevel.Error, ex.StackTrace ?? "", sessionId: request.SessionId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetSessions(
        ConversationService conversationService,
        DiagnosticsService diagnosticsService,
        int days = 30,
        int page = 1,
        int pageSize = 50,
        bool includeDeleted = false)
    {
        try
        {
            var sessions = await conversationService.GetSessionsAsync(days, page, pageSize, includeDeleted);
            var dtos = sessions.Select(s => new SessionDto(
                s.SessionId,
                s.Tool,
                s.CreatedAt,
                s.Count,
                s.IsDeleted
            ));
            return Results.Ok(dtos);
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.GetSessions", ex.Message, LogLevel.Error, ex.StackTrace ?? "");
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateSessionDeleted(
        string sessionId,
        UpdateSessionDeletedRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateSessionDeletedAsync(sessionId, request.IsDeleted);
            return Results.NoContent();
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateSessionDeleted", ex.Message, LogLevel.Error, ex.StackTrace ?? "", sessionId: sessionId);
            return Results.Problem(ex.Message);
        }
    }
}
