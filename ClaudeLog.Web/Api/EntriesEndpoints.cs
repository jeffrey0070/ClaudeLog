using ClaudeLog.Data.Services;
using ClaudeLog.Web.Api.Dtos;
using ClaudeLog.Web.Services;
using LogLevel = ClaudeLog.Data.Models.LogLevel;

namespace ClaudeLog.Web.Api;

/// <summary>
/// API endpoints for managing conversation entries.
/// Provides CRUD operations and filtering for conversations.
/// </summary>
public static class EntriesEndpoints
{
    public static void MapEntriesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/entries");

        group.MapPost("/", CreateEntry);
        group.MapGet("/", GetEntries);
        group.MapGet("/{conversationId:guid}", GetEntryById);
        group.MapPatch("/{conversationId:guid}/title", UpdateTitle);
        group.MapPatch("/{conversationId:guid}/favorite", UpdateFavorite);
        group.MapPatch("/{conversationId:guid}/deleted", UpdateDeleted);
        group.MapPatch("/{conversationId:guid}/question", UpdateQuestion);
        group.MapPatch("/{conversationId:guid}/response", UpdateResponse);
        group.MapPatch("/{conversationId:guid}/session", UpdateSessionId);
    }

    /// <summary>
    /// Creates a new conversation entry.
    /// Auto-generates title from question text and trims whitespace.
    /// </summary>
    private static async Task<IResult> CreateEntry(
        CreateEntryRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            var conversationId = await conversationService.WriteEntryAsync(request.SessionId, request.Question, request.Response);
            return Results.Ok(new CreateEntryResponse(conversationId));
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.CreateEntry", ex.Message, LogLevel.Error, ex.StackTrace ?? "", sessionId: request.SessionId);
            return Results.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Gets paginated list of conversation entries with optional filtering.
    /// Supports search, deleted/favorite filters, and pagination.
    /// </summary>
    private static async Task<IResult> GetEntries(
        ConversationService conversationService,
        DiagnosticsService diagnosticsService,
        string? search = null,
        int page = 1,
        int pageSize = 200,
        bool includeDeleted = false,
        bool showFavoritesOnly = false)
    {
        try
        {
            var entries = await conversationService.GetEntriesAsync(search, includeDeleted, showFavoritesOnly, page, pageSize);
            var dtos = entries.Select(e => new EntryListDto(
                e.ConversationId,
                e.Title,
                e.CreatedAt,
                e.SessionId,
                e.SessionCreatedAt,
                e.Tool,
                e.IsFavorite,
                e.IsDeleted,
                e.SessionIsDeleted
            ));
            return Results.Ok(dtos);
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.GetEntries", ex.Message, LogLevel.Error, ex.StackTrace ?? "");
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetEntryById(
        Guid conversationId,
        ConversationService conversationService,
        MarkdownRenderer markdownRenderer,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            var entry = await conversationService.GetEntryByIdAsync(conversationId);
            if (entry != null)
            {
                var dto = new EntryDetailDto(
                    entry.ConversationId,
                    entry.Title,
                    entry.Question,
                    markdownRenderer.ToHtml(entry.Question),
                    entry.Response,
                    markdownRenderer.ToHtml(entry.Response),
                    entry.CreatedAt,
                    entry.SessionId,
                    entry.Tool,
                    entry.SessionCreatedAt,
                    entry.IsFavorite,
                    entry.IsDeleted
                );
                return Results.Ok(dto);
            }

            return Results.NotFound();
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.GetEntryById", ex.Message, LogLevel.Error, ex.StackTrace ?? "", conversationId: conversationId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateTitle(
        Guid conversationId,
        UpdateTitleRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateTitleAsync(conversationId, request.Title);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateTitle", ex.Message, LogLevel.Error, ex.StackTrace ?? "", conversationId: conversationId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateFavorite(
        Guid conversationId,
        UpdateFavoriteRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateFavoriteAsync(conversationId, request.IsFavorite);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateFavorite", ex.Message, LogLevel.Error, ex.StackTrace ?? "", conversationId: conversationId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateDeleted(
        Guid conversationId,
        UpdateDeletedRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateDeletedAsync(conversationId, request.IsDeleted);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateDeleted", ex.Message, LogLevel.Error, ex.StackTrace ?? "", conversationId: conversationId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateQuestion(
        Guid conversationId,
        UpdateQuestionRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateQuestionAsync(conversationId, request.Question);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateQuestion", ex.Message, LogLevel.Error, ex.StackTrace ?? "", conversationId: conversationId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateResponse(
        Guid conversationId,
        UpdateResponseRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateResponseAsync(conversationId, request.Response);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateResponse", ex.Message, LogLevel.Error, ex.StackTrace ?? "", conversationId: conversationId);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateSessionId(
        Guid conversationId,
        UpdateSessionIdRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateSessionIdAsync(conversationId, request.SessionId);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateSessionId", ex.Message, LogLevel.Error, ex.StackTrace ?? "", conversationId: conversationId);
            return Results.Problem(ex.Message);
        }
    }
}
