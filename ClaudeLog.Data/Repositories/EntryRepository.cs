using ClaudeLog.Data.Models;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data.Repositories;

/// <summary>
/// Repository for managing conversation entries (Q&A pairs)
/// </summary>
public class EntryRepository
{
    private readonly DbContext _dbContext;

    public EntryRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Creates a new conversation entry (question and response pair).
    /// Automatically generates a title from the first 100 characters of the question.
    /// </summary>
    /// <param name="sessionId">The session ID this entry belongs to (required)</param>
    /// <param name="question">The user's question/prompt (required)</param>
    /// <param name="response">The assistant's response (required)</param>
    /// <returns>The ID of the newly created entry</returns>
    /// <exception cref="ArgumentException">Thrown if any required parameter is null/empty</exception>
    public async Task<long> CreateAsync(string sessionId, string question, string response)
    {
        // Validate required parameters
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be null or empty", nameof(question));
        if (string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("Response cannot be null or empty", nameof(response));

        // Normalize inputs
        var normalizedSessionId = sessionId.Trim();
        var normalizedQuestion = question.Trim();
        var normalizedResponse = response.Trim();

        // Generate title from question (max 100 chars with ellipsis)
        var title = normalizedQuestion.Length > 100
            ? normalizedQuestion[..97] + "..."
            : normalizedQuestion;

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            INSERT INTO dbo.Conversations (SessionId, Title, Question, Response, CreatedAt)
            OUTPUT INSERTED.Id
            VALUES (@SessionId, @Title, @Question, @Response, @CreatedAt)";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@SessionId", normalizedSessionId);
        cmd.Parameters.AddWithValue("@Title", title);
        cmd.Parameters.AddWithValue("@Question", normalizedQuestion);
        cmd.Parameters.AddWithValue("@Response", normalizedResponse);
        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

        var result = await cmd.ExecuteScalarAsync();

        // ExecuteScalar should never return null for OUTPUT INSERTED.Id, but be defensive
        if (result == null || result == DBNull.Value)
            throw new InvalidOperationException("Failed to retrieve the inserted entry ID");

        return (long)result;
    }

    /// <summary>
    /// Retrieves a paginated list of conversation entries with optional filtering
    /// </summary>
    /// <param name="search">Search term to filter by title, question, or response (optional)</param>
    /// <param name="includeDeleted">Whether to include soft-deleted entries (default: false)</param>
    /// <param name="showFavoritesOnly">Show only favorited entries (default: false)</param>
    /// <param name="page">Page number, starting at 1 (default: 1)</param>
    /// <param name="pageSize">Number of entries per page (default: 50)</param>
    /// <returns>List of entry summaries ordered by session and entry creation date (newest first)</returns>
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

        var query = @"
            SELECT c.Id, c.Title, c.CreatedAt, c.SessionId, s.CreatedAt as SessionCreatedAt, s.Tool,
                   c.IsFavorite, c.IsDeleted, s.IsDeleted as SessionIsDeleted
            FROM dbo.Conversations c
            INNER JOIN dbo.Sessions s ON c.SessionId = s.SessionId
            WHERE (@Search IS NULL OR @Search = '' OR
                   c.Title LIKE @SearchPattern OR
                   c.Question LIKE @SearchPattern OR
                   c.Response LIKE @SearchPattern)
              AND (@IncludeDeleted = 1 OR c.IsFavorite = 1 OR (c.IsDeleted = 0 AND s.IsDeleted = 0))
              AND (@ShowFavoritesOnly = 0 OR c.IsFavorite = 1)
            ORDER BY s.CreatedAt DESC, c.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        using var cmd = new SqlCommand(query, conn);
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
                reader.GetInt64(0),       // Id - never null (PK)
                reader.GetString(1),      // Title - never null (NOT NULL)
                reader.GetDateTime(2),    // CreatedAt - never null (NOT NULL)
                reader.GetString(3),      // SessionId - never null (FK to PK)
                reader.GetDateTime(4),    // SessionCreatedAt - never null (NOT NULL)
                reader.GetString(5),      // Tool - never null (NOT NULL)
                reader.GetBoolean(6),     // IsFavorite - never null (NOT NULL with DEFAULT)
                reader.GetBoolean(7),     // IsDeleted - never null (NOT NULL with DEFAULT)
                reader.GetBoolean(8)      // SessionIsDeleted - never null (NOT NULL with DEFAULT)
            ));
        }

        return entries;
    }

    /// <summary>
    /// Retrieves the full details of a single conversation entry by ID
    /// </summary>
    /// <param name="id">The entry ID to retrieve</param>
    /// <returns>The entry details, or null if not found</returns>
    public async Task<EntryDetail?> GetEntryByIdAsync(long id)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            SELECT c.Id, c.Title, c.Question, c.Response, c.CreatedAt,
                   c.SessionId, s.Tool, s.CreatedAt as SessionCreatedAt,
                   c.IsFavorite, c.IsDeleted
            FROM dbo.Conversations c
            INNER JOIN dbo.Sessions s ON c.SessionId = s.SessionId
            WHERE c.Id = @Id";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new EntryDetail(
                reader.GetInt64(0),       // Id - never null (PK)
                reader.GetString(1),      // Title - never null (NOT NULL)
                reader.GetString(2),      // Question - never null (NOT NULL)
                reader.GetString(3),      // Response - never null (NOT NULL)
                reader.GetDateTime(4),    // CreatedAt - never null (NOT NULL)
                reader.GetString(5),      // SessionId - never null (FK to PK)
                reader.GetString(6),      // Tool - never null (NOT NULL)
                reader.GetDateTime(7),    // SessionCreatedAt - never null (NOT NULL)
                reader.GetBoolean(8),     // IsFavorite - never null (NOT NULL with DEFAULT)
                reader.GetBoolean(9)      // IsDeleted - never null (NOT NULL with DEFAULT)
            );
        }

        return null;
    }

    /// <summary>
    /// Updates the title of a conversation entry
    /// </summary>
    /// <param name="id">The entry ID to update</param>
    /// <param name="title">The new title (required)</param>
    public async Task UpdateTitleAsync(long id, string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be null or empty", nameof(title));

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            UPDATE dbo.Conversations
            SET Title = @Title
            WHERE Id = @Id";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Title", title.Trim());

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Toggles the favorite status of a conversation entry
    /// </summary>
    /// <param name="id">The entry ID to update</param>
    /// <param name="isFavorite">True to mark as favorite, false to unmark</param>
    public async Task UpdateFavoriteAsync(long id, bool isFavorite)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            UPDATE dbo.Conversations
            SET IsFavorite = @IsFavorite
            WHERE Id = @Id";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@IsFavorite", isFavorite ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Soft deletes or restores a conversation entry by setting its IsDeleted flag
    /// </summary>
    /// <param name="id">The entry ID to update</param>
    /// <param name="isDeleted">True to soft-delete, false to restore</param>
    public async Task UpdateDeletedAsync(long id, bool isDeleted)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            UPDATE dbo.Conversations
            SET IsDeleted = @IsDeleted
            WHERE Id = @Id";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@IsDeleted", isDeleted ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Updates the question of a conversation entry
    /// </summary>
    /// <param name="id">The entry ID to update</param>
    /// <param name="question">The new question text (required)</param>
    public async Task UpdateQuestionAsync(long id, string question)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question cannot be null or empty", nameof(question));

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            UPDATE dbo.Conversations
            SET Question = @Question
            WHERE Id = @Id";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Question", question.Trim());

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Updates the response of a conversation entry
    /// </summary>
    /// <param name="id">The entry ID to update</param>
    /// <param name="response">The new response text (required)</param>
    public async Task UpdateResponseAsync(long id, string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            throw new ArgumentException("Response cannot be null or empty", nameof(response));

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            UPDATE dbo.Conversations
            SET Response = @Response
            WHERE Id = @Id";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Response", response.Trim());

        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Gets the total count of conversation entries matching the search criteria
    /// </summary>
    /// <param name="search">Search term to filter by (optional)</param>
    /// <returns>Total number of matching entries</returns>
    public async Task<int> GetCountAsync(string? search = null)
    {
        var searchPattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%";

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            SELECT COUNT(*)
            FROM dbo.Conversations c
            INNER JOIN dbo.Sessions s ON c.SessionId = s.SessionId
            WHERE (@Search IS NULL OR @Search = '' OR
                   c.Title LIKE @SearchPattern OR
                   c.Question LIKE @SearchPattern OR
                   c.Response LIKE @SearchPattern)";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Search", (object?)search ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SearchPattern", (object?)searchPattern ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();

        // ExecuteScalar with COUNT(*) should never return null, but be defensive
        if (result == null || result == DBNull.Value)
            return 0;

        return (int)result;
    }
}
