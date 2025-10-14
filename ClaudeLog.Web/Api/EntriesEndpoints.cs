using ClaudeLog.Data.Services;
using ClaudeLog.Web.Api.Dtos;

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
        LoggingService service)
    {
        try
        {
            var (success, entryId) = await service.LogEntryAsync(request.SessionId, request.Question, request.Response);
            if (success && entryId.HasValue)
            {
                return Results.Ok(new CreateEntryResponse(entryId.Value));
            }
            return Results.Problem("Failed to create entry");
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    /// <summary>
    /// Gets paginated list of conversation entries with optional filtering.
    /// Supports search, deleted/favorite filters, and pagination.
    /// </summary>
    private static async Task<IResult> GetEntries(
        LoggingService service,
        string? search = null,
        int page = 1,
        int pageSize = 200,
        bool includeDeleted = false,
        bool showFavoritesOnly = false)
    {
        try
        {
            var entries = await service.GetEntriesAsync(search, includeDeleted, showFavoritesOnly, page, pageSize);
            return Results.Ok(entries);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetEntryById(
        long id,
        LoggingService service)
    {
        try
        {
            var entry = await service.GetEntryByIdAsync(id);
            if (entry != null)
            {
                return Results.Ok(entry);
            }

            return Results.NotFound();
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateTitle(
        long id,
        UpdateTitleRequest request,
        LoggingService service)
    {
        try
        {
            await service.UpdateTitleAsync(id, request.Title);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateFavorite(
        long id,
        UpdateFavoriteRequest request,
        LoggingService service)
    {
        try
        {
            await service.UpdateFavoriteAsync(id, request.IsFavorite);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> UpdateDeleted(
        long id,
        UpdateDeletedRequest request,
        LoggingService service)
    {
        try
        {
            await service.UpdateDeletedAsync(id, request.IsDeleted);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
