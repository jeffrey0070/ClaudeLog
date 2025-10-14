using ClaudeLog.Data.Models;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data.Repositories;

/// <summary>
/// Repository for logging and managing error records
/// </summary>
public class ErrorRepository
{
    private readonly DbContext _dbContext;

    public ErrorRepository(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Logs an error to the database with optional contextual information.
    /// This method is designed to be fail-safe - it should not throw exceptions.
    /// </summary>
    /// <param name="source">Source of the error (e.g., "Hook.Claude", "MCP.Server") - required</param>
    /// <param name="message">Error message - required</param>
    /// <param name="detail">Detailed information such as stack trace (optional)</param>
    /// <param name="path">File path related to the error (optional)</param>
    /// <param name="sessionId">Associated session ID (optional)</param>
    /// <param name="entryId">Associated entry ID (optional)</param>
    /// <param name="createdAt">Timestamp of the error. Defaults to current time if null.</param>
    /// <param name="logLevel">Severity level of the log entry. Defaults to Error.</param>
    /// <returns>The ID of the logged error entry, or 0 if logging failed</returns>
    public async Task<long> LogErrorAsync(
        string source,
        string message,
        string? detail = null,
        string? path = null,
        string? sessionId = null,
        long? entryId = null,
        DateTime? createdAt = null,
        LogLevel logLevel = LogLevel.Error)
    {
        try
        {
            // Validate and normalize required parameters
            if (string.IsNullOrWhiteSpace(source))
                source = "Unknown";  // Fallback to prevent null constraint violation
            if (string.IsNullOrWhiteSpace(message))
                message = "No error message provided";  // Fallback to prevent null constraint violation

            var normalizedSource = source.Trim();
            var normalizedMessage = message.Trim();
            var created = createdAt ?? DateTime.Now;

            // Truncate if necessary to fit column constraints
            if (normalizedSource.Length > 64)
                normalizedSource = normalizedSource[..64];
            if (normalizedMessage.Length > 1024)
                normalizedMessage = normalizedMessage[..1021] + "...";
            if (path != null && path.Length > 256)
                path = path[..253] + "...";

            using var conn = _dbContext.CreateConnection();
            await conn.OpenAsync();

            var query = @"
                INSERT INTO dbo.ErrorLogs (Source, Message, Detail, Path, SessionId, EntryId, CreatedAt, LogLevel)
                OUTPUT INSERTED.Id
                VALUES (@Source, @Message, @Detail, @Path, @SessionId, @EntryId, @CreatedAt, @LogLevel)";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@Source", normalizedSource);
            cmd.Parameters.AddWithValue("@Message", normalizedMessage);
            cmd.Parameters.AddWithValue("@Detail", (object?)detail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Path", (object?)path ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SessionId", (object?)sessionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EntryId", entryId.HasValue ? (object)entryId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", created);
            cmd.Parameters.AddWithValue("@LogLevel", (int)logLevel);

            var result = await cmd.ExecuteScalarAsync();

            // Return the inserted ID, or 0 if somehow null
            if (result == null || result == DBNull.Value)
                return 0;

            return (long)result;
        }
        catch
        {
            // Swallow all exceptions to prevent error logging from causing cascading failures
            // This is intentional - error logging should never break the application
            return 0;
        }
    }

    /// <summary>
    /// Retrieves a paginated list of error logs with optional filtering
    /// </summary>
    /// <param name="source">Filter by error source (optional)</param>
    /// <param name="days">Number of days to look back (default: 7)</param>
    /// <param name="page">Page number, starting at 1 (default: 1)</param>
    /// <param name="pageSize">Number of errors per page (default: 50)</param>
    /// <returns>List of error logs ordered by creation date (newest first)</returns>
    public async Task<List<ErrorLog>> GetErrorsAsync(
        string? source = null,
        int days = 7,
        int page = 1,
        int pageSize = 50)
    {
        var errors = new List<ErrorLog>();

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            SELECT Id, Source, Message, Detail, Path, SessionId, EntryId, CreatedAt, LogLevel
            FROM dbo.ErrorLogs
            WHERE CreatedAt >= DATEADD(DAY, -@Days, SYSDATETIME())
              AND (@Source IS NULL OR Source = @Source)
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Days", days);
        cmd.Parameters.AddWithValue("@Source", (object?)source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            errors.Add(new ErrorLog(
                reader.GetInt64(0),                                        // Id - never null (PK)
                reader.GetString(1),                                       // Source - never null (NOT NULL)
                reader.GetString(2),                                       // Message - never null (NOT NULL)
                reader.IsDBNull(3) ? null : reader.GetString(3),          // Detail - nullable
                reader.IsDBNull(4) ? null : reader.GetString(4),          // Path - nullable
                reader.IsDBNull(5) ? null : reader.GetString(5),          // SessionId - nullable
                reader.IsDBNull(6) ? null : reader.GetInt64(6),           // EntryId - nullable
                reader.GetDateTime(7),                                     // CreatedAt - never null (NOT NULL)
                (LogLevel)reader.GetInt32(8)                               // LogLevel - never null (NOT NULL with DEFAULT)
            ));
        }

        return errors;
    }

    /// <summary>
    /// Gets the total count of error logs matching the criteria
    /// </summary>
    /// <param name="source">Filter by error source (optional)</param>
    /// <param name="days">Number of days to look back (default: 7)</param>
    /// <returns>Total number of matching error logs</returns>
    public async Task<int> GetCountAsync(string? source = null, int days = 7)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            SELECT COUNT(*)
            FROM dbo.ErrorLogs
            WHERE CreatedAt >= DATEADD(DAY, -@Days, SYSDATETIME())
              AND (@Source IS NULL OR Source = @Source)";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@Days", days);
        cmd.Parameters.AddWithValue("@Source", (object?)source ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();

        // ExecuteScalar with COUNT(*) should never return null, but be defensive
        if (result == null || result == DBNull.Value)
            return 0;

        return (int)result;
    }
}
