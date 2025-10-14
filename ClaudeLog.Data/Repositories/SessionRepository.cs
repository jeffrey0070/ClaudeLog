using ClaudeLog.Data.Models;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data.Repositories;

/// <summary>
/// Repository for managing conversation sessions
/// </summary>
public class SessionRepository
{
    private readonly DbContext _dbContext;

    public SessionRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Gets an existing session or creates a new one if it doesn't exist.
    /// This is idempotent - calling multiple times with the same sessionId will return the same session.
    /// </summary>
    /// <param name="sessionId">Unique identifier for the session. If null/empty, a new GUID will be generated.</param>
    /// <param name="tool">Tool/application name (e.g., "ClaudeCode", "Codex"). Defaults to empty string if null.</param>
    /// <param name="createdAt">Creation timestamp. Defaults to current time if null.</param>
    /// <returns>The session ID (either provided or generated)</returns>
    public async Task<string> GetOrCreateAsync(string? sessionId = null, string? tool = null, DateTime? createdAt = null)
    {
        // Normalize inputs - never allow null values to reach the database
        var id = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString()
            : sessionId.Trim();

        var toolValue = string.IsNullOrWhiteSpace(tool)
            ? string.Empty
            : tool.Trim();

        var created = createdAt ?? DateTime.Now;

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        // Use MERGE to make this idempotent - if session exists, do nothing; otherwise insert
        var query = @"
            MERGE dbo.Sessions AS target
            USING (SELECT @SessionId AS SessionId, @Tool AS Tool, @CreatedAt AS CreatedAt) AS source
            ON target.SessionId = source.SessionId
            WHEN NOT MATCHED THEN
                INSERT (SessionId, Tool, CreatedAt)
                VALUES (source.SessionId, source.Tool, source.CreatedAt);

            SELECT @SessionId;";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@SessionId", id);
        cmd.Parameters.AddWithValue("@Tool", toolValue);
        cmd.Parameters.AddWithValue("@CreatedAt", created);

        // ExecuteScalar returns the session ID whether it was inserted or already existed
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? id; // Fallback to id if somehow null
    }

    /// <summary>
    /// Retrieves a paginated list of sessions with conversation counts
    /// </summary>
    /// <param name="days">Number of days to look back (default: 30)</param>
    /// <param name="page">Page number, starting at 1 (default: 1)</param>
    /// <param name="pageSize">Number of sessions per page (default: 50)</param>
    /// <param name="includeDeleted">Whether to include soft-deleted sessions (default: false)</param>
    /// <returns>List of sessions ordered by creation date (newest first)</returns>
    public async Task<List<Session>> GetSessionsAsync(int days = 30, int page = 1, int pageSize = 50, bool includeDeleted = false)
    {
        var sessions = new List<Session>();

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            SELECT s.SessionId, s.Tool, s.CreatedAt, COUNT(c.Id) as Count, s.IsDeleted
            FROM dbo.Sessions s
            LEFT JOIN dbo.Conversations c ON s.SessionId = c.SessionId
            WHERE s.CreatedAt >= DATEADD(DAY, -@Days, SYSDATETIME())
              AND (@IncludeDeleted = 1 OR s.IsDeleted = 0)
            GROUP BY s.SessionId, s.Tool, s.CreatedAt, s.IsDeleted
            ORDER BY s.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Days", days);
        cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);
        cmd.Parameters.AddWithValue("@IncludeDeleted", includeDeleted ? 1 : 0);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(new Session(
                reader.GetString(0),      // SessionId - never null (PK)
                reader.GetString(1),      // Tool - never null (NOT NULL)
                reader.GetDateTime(2),    // CreatedAt - never null (NOT NULL)
                reader.GetInt32(3),       // Count - never null (aggregate)
                reader.GetBoolean(4)      // IsDeleted - never null (NOT NULL with DEFAULT)
            ));
        }

        return sessions;
    }

    /// <summary>
    /// Soft deletes or restores a session by setting its IsDeleted flag
    /// </summary>
    /// <param name="sessionId">The session ID to update</param>
    /// <param name="isDeleted">True to soft-delete, false to restore</param>
    public async Task UpdateDeletedAsync(string sessionId, bool isDeleted)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            UPDATE dbo.Sessions
            SET IsDeleted = @IsDeleted
            WHERE SessionId = @SessionId";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@SessionId", sessionId.Trim());
        cmd.Parameters.AddWithValue("@IsDeleted", isDeleted ? 1 : 0);

        await cmd.ExecuteNonQueryAsync();
    }
}
