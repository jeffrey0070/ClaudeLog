using ClaudeLog.Web.Api.Dtos;
using ClaudeLog.Web.Data;
using ClaudeLog.Web.Services;
using Microsoft.Data.SqlClient;

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
        Db db)
    {
        try
        {
            var sectionId = Guid.Parse(request.SectionId);
            var question = request.Question?.Trim() ?? "";
            var response = request.Response?.Trim() ?? "";
            var title = TitleGenerator.MakeTitle(question);
            var createdAt = DateTime.Now;

            using var conn = db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(Queries.InsertConversation, conn);
            cmd.Parameters.AddWithValue("@SectionId", sectionId);
            cmd.Parameters.AddWithValue("@Title", title);
            cmd.Parameters.AddWithValue("@Question", question);
            cmd.Parameters.AddWithValue("@Response", response);
            cmd.Parameters.AddWithValue("@CreatedAt", createdAt);

            var id = await cmd.ExecuteScalarAsync();

            return Results.Ok(new CreateEntryResponse(Convert.ToInt64(id)));
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
        Db db,
        string? search = null,
        int page = 1,
        int pageSize = 200,
        bool includeDeleted = false,
        bool showFavoritesOnly = false)
    {
        try
        {
            var entries = new List<EntryListDto>();
            var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

            using var conn = db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(Queries.GetConversations, conn);
            cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SearchPattern", (object?)searchPattern ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
            cmd.Parameters.AddWithValue("@PageSize", pageSize);
            cmd.Parameters.AddWithValue("@IncludeDeleted", includeDeleted);
            cmd.Parameters.AddWithValue("@ShowFavoritesOnly", showFavoritesOnly);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                entries.Add(new EntryListDto(
                    reader.GetInt64(0),      // Id
                    reader.GetString(1),     // Title
                    reader.GetDateTime(2),   // CreatedAt
                    reader.GetGuid(3),       // SectionId
                    reader.GetDateTime(4),   // SectionCreatedAt
                    reader.GetString(5),     // Tool
                    reader.GetBoolean(6),    // IsFavorite
                    reader.GetBoolean(7)     // IsDeleted
                ));
            }

            return Results.Ok(entries);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> GetEntryById(
        long id,
        Db db)
    {
        try
        {
            using var conn = db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(Queries.GetConversationById, conn);
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var entry = new EntryDetailDto(
                    reader.GetInt64(0),      // Id
                    reader.GetString(1),     // Title
                    reader.GetString(2),     // Question
                    reader.GetString(3),     // Response
                    reader.GetDateTime(4),   // CreatedAt
                    reader.GetGuid(5),       // SectionId
                    reader.GetString(6),     // Tool
                    reader.GetDateTime(7),   // SectionCreatedAt
                    reader.GetBoolean(8),    // IsFavorite
                    reader.GetBoolean(9)     // IsDeleted
                );

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
        Db db)
    {
        try
        {
            using var conn = db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(Queries.UpdateConversationTitle, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Title", request.Title);

            await cmd.ExecuteNonQueryAsync();

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
        Db db)
    {
        try
        {
            using var conn = db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(Queries.UpdateConversationFavorite, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@IsFavorite", request.IsFavorite);

            await cmd.ExecuteNonQueryAsync();

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
        Db db)
    {
        try
        {
            using var conn = db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(Queries.UpdateConversationDeleted, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@IsDeleted", request.IsDeleted);

            await cmd.ExecuteNonQueryAsync();

            return Results.Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
