using ClaudeLog.Data.Models;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data.Repositories;

public class EntryRepository
{
    private readonly DbContext _dbContext;

    private const string InsertConversationQuery = @"
        INSERT INTO dbo.Conversations (SectionId, Title, Question, Response, CreatedAt)
        OUTPUT INSERTED.Id
        VALUES (@SectionId, @Title, @Question, @Response, @CreatedAt)";

    private const string GetConversationsQuery = @"
        SELECT c.Id, c.Title, c.CreatedAt, c.SectionId, s.CreatedAt as SectionCreatedAt, s.Tool,
               c.IsFavorite, c.IsDeleted
        FROM dbo.Conversations c
        INNER JOIN dbo.Sections s ON c.SectionId = s.SectionId
        WHERE (@Search IS NULL OR @Search = '' OR
               c.Title LIKE @SearchPattern OR
               c.Question LIKE @SearchPattern OR
               c.Response LIKE @SearchPattern)
          AND (@IncludeDeleted = 1 OR c.IsDeleted = 0)
          AND (@ShowFavoritesOnly = 0 OR c.IsFavorite = 1)
        ORDER BY s.CreatedAt DESC, c.CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

    private const string GetConversationByIdQuery = @"
        SELECT c.Id, c.Title, c.Question, c.Response, c.CreatedAt,
               c.SectionId, s.Tool, s.CreatedAt as SectionCreatedAt,
               c.IsFavorite, c.IsDeleted
        FROM dbo.Conversations c
        INNER JOIN dbo.Sections s ON c.SectionId = s.SectionId
        WHERE c.Id = @Id";

    private const string UpdateConversationTitleQuery = @"
        UPDATE dbo.Conversations
        SET Title = @Title
        WHERE Id = @Id";

    private const string UpdateConversationFavoriteQuery = @"
        UPDATE dbo.Conversations
        SET IsFavorite = @IsFavorite
        WHERE Id = @Id";

    private const string UpdateConversationDeletedQuery = @"
        UPDATE dbo.Conversations
        SET IsDeleted = @IsDeleted
        WHERE Id = @Id";

    private const string GetConversationsCountQuery = @"
        SELECT COUNT(*)
        FROM dbo.Conversations c
        INNER JOIN dbo.Sections s ON c.SectionId = s.SectionId
        WHERE (@Search IS NULL OR @Search = '' OR
               c.Title LIKE @SearchPattern OR
               c.Question LIKE @SearchPattern OR
               c.Response LIKE @SearchPattern)";

    public EntryRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateEntryResponse> CreateAsync(CreateEntryRequest request)
    {
        var sectionId = Guid.Parse(request.SectionId);

        // Generate title from question
        var title = request.Question.Length > 100
            ? request.Question[..97] + "..."
            : request.Question;

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(InsertConversationQuery, conn);
        cmd.Parameters.AddWithValue("@SectionId", sectionId);
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@Question", request.Question);
        cmd.Parameters.AddWithValue("@Response", request.Response);
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

        var id = (long)await cmd.ExecuteScalarAsync();

        return new CreateEntryResponse(id);
    }

    public async Task<List<EntryListItem>> GetEntriesAsync(
        string? search = null,
        bool includeDeleted = false,
        bool showFavoritesOnly = false,
        int page = 1,
        int pageSize = 50)
    {
        var entries = new List<EntryListItem>();
        var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(GetConversationsQuery, conn);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SearchPattern", (object?)searchPattern ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IncludeDeleted", includeDeleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@ShowFavoritesOnly", showFavoritesOnly ? 1 : 0);
        cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            entries.Add(new EntryListItem(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetDateTime(2),
                reader.GetGuid(3),
                reader.GetDateTime(4),
                reader.GetString(5),
                reader.GetBoolean(6),
                reader.GetBoolean(7)
            ));
        }

        return entries;
    }

    public async Task<EntryDetail?> GetEntryByIdAsync(long id)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(GetConversationByIdQuery, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new EntryDetail(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDateTime(4),
                reader.GetGuid(5),
                reader.GetString(6),
                reader.GetDateTime(7),
                reader.GetBoolean(8),
                reader.GetBoolean(9)
            );
        }

        return null;
    }

    public async Task UpdateTitleAsync(long id, string title)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(UpdateConversationTitleQuery, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Title", title);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateFavoriteAsync(long id, bool isFavorite)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(UpdateConversationFavoriteQuery, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@IsFavorite", isFavorite);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateDeletedAsync(long id, bool isDeleted)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(UpdateConversationDeletedQuery, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@IsDeleted", isDeleted);

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> GetCountAsync(string? search = null)
    {
        var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(GetConversationsCountQuery, conn);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SearchPattern", (object?)searchPattern ?? DBNull.Value);

        return (int)await cmd.ExecuteScalarAsync();
    }
}
