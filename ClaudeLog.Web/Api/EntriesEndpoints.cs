using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

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
        EntryRepository repository)
    {
        try
        {
            var response = await repository.CreateAsync(request);
            return Results.Ok(response);
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
        EntryRepository repository,
        string? search = null,
        int page = 1,
        int pageSize = 200,
        bool includeDeleted = false,
        bool showFavoritesOnly = false)
    {
        try
        {
            var entries = await repository.GetEntriesAsync(search, includeDeleted, showFavoritesOnly, page, pageSize);
            return Results.Ok(entries);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetEntryById(
        long id,
        EntryRepository repository)
    {
        try
        {
            var entry = await repository.GetEntryByIdAsync(id);
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
        EntryRepository repository)
    {
        try
        {
            await repository.UpdateTitleAsync(id, request.Title);
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
        EntryRepository repository)
    {
        try
        {
            await repository.UpdateFavoriteAsync(id, request.IsFavorite);
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
        EntryRepository repository)
    {
        try
        {
            await repository.UpdateDeletedAsync(id, request.IsDeleted);
            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
