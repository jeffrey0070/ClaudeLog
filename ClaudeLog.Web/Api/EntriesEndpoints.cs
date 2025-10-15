using ClaudeLog.Data.Services;
using ClaudeLog.Web.Api.Dtos;
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
        group.MapGet("/{id}", GetEntryById);
        group.MapPatch("/{id}/title", UpdateTitle);
        group.MapPatch("/{id}/favorite", UpdateFavorite);
        group.MapPatch("/{id}/deleted", UpdateDeleted);
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
            var entryId = await conversationService.WriteEntryAsync(request.SessionId, request.Question, request.Response);
            return Results.Ok(new CreateEntryResponse(entryId));
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
                e.Id,
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
        long id,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            var entry = await conversationService.GetEntryByIdAsync(id);
            if (entry != null)
            {
                var dto = new EntryDetailDto(
                    entry.Id,
                    entry.Title,
                    entry.Question,
                    entry.Response,
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
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.GetEntryById", ex.Message, LogLevel.Error, ex.StackTrace ?? "", entryId: id);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateTitle(
        long id,
        UpdateTitleRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateTitleAsync(id, request.Title);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateTitle", ex.Message, LogLevel.Error, ex.StackTrace ?? "", entryId: id);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateFavorite(
        long id,
        UpdateFavoriteRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateFavoriteAsync(id, request.IsFavorite);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateFavorite", ex.Message, LogLevel.Error, ex.StackTrace ?? "", entryId: id);
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateDeleted(
        long id,
        UpdateDeletedRequest request,
        ConversationService conversationService,
        DiagnosticsService diagnosticsService)
    {
        try
        {
            await conversationService.UpdateDeletedAsync(id, request.IsDeleted);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            await diagnosticsService.WriteDiagnosticsAsync("WebApi.UpdateDeleted", ex.Message, LogLevel.Error, ex.StackTrace ?? "", entryId: id);
            return Results.Problem(ex.Message);
        }
    }
}
