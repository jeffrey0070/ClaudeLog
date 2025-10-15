using ClaudeLog.Data.Models;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data.Repositories;

/// <summary>
/// Repository for logging and managing error records
/// </summary>
public class DiagnosticsRepository
{
    private readonly DbContext _dbContext;

    public DiagnosticsRepository(DbContext dbContext)
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
        catch (Exception ex)
        {
            // Fallback: write to local daily log file in the app folder. Swallow all exceptions here.
            SafeLogToFile(source, message, detail, path, sessionId, entryId, createdAt ?? DateTime.Now, logLevel, ex);
            return 0;
        }
    }

    private void SafeLogToFile(
        string source,
        string message,
        string? detail,
        string? path,
        string? sessionId,
        long? entryId,
        DateTime createdAt,
        LogLevel level,
        Exception exception)
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            // Ensure baseDir exists (normally true), but be defensive
            if (!Directory.Exists(baseDir))
            {
                Directory.CreateDirectory(baseDir);
            }

            var filePath = System.IO.Path.Combine(baseDir, $"Log_{DateTime.Now:yyyyMMdd}.txt");
            var now = DateTime.Now;

            using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(stream);

            writer.WriteLine(new string('-', 80));
            writer.WriteLine($"[{now:yyyy-MM-dd HH:mm:ss.fff}] DiagnosticsRepository fallback log");
            writer.WriteLine($"Level: {level}  Source: {source}");
            writer.WriteLine($"Message: {message}");
            if (!string.IsNullOrWhiteSpace(detail)) writer.WriteLine($"Detail: {detail}");
            if (!string.IsNullOrWhiteSpace(path)) writer.WriteLine($"Path: {path}");
            if (!string.IsNullOrWhiteSpace(sessionId)) writer.WriteLine($"SessionId: {sessionId}");
            if (entryId.HasValue) writer.WriteLine($"EntryId: {entryId}");
            writer.WriteLine($"CreatedAt: {createdAt:O}");
            writer.WriteLine("DB logging failed with exception:");
            writer.WriteLine(exception.ToString());
            writer.Flush();
        }
        catch
        {
            // Intentionally swallow any exceptions during file fallback logging.
        }
    }

    public async Task<List<(long Id, string Source, string Message, string? Detail, string? Path, string? SessionId, long? EntryId, DateTime CreatedAt, LogLevel LogLevel)>> GetLogsAsync(
        LogLevel? minLevel = null,
        string? source = null,
        int page = 1,
        int pageSize = 100)
    {
        var list = new List<(long, string, string, string?, string?, string?, long?, DateTime, LogLevel)>();

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        var query = @"
            SELECT Id, Source, Message, Detail, Path, SessionId, EntryId, CreatedAt, LogLevel
            FROM dbo.ErrorLogs
            WHERE (@MinLevel IS NULL OR LogLevel >= @MinLevel)
              AND (@Source IS NULL OR @Source = '' OR Source = @Source)
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        using var cmd = new SqlCommand(query, conn);
        cmd.Parameters.AddWithValue("@MinLevel", minLevel.HasValue ? (object)(int)minLevel.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@Source", (object?)source ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
        cmd.Parameters.AddWithValue("@PageSize", pageSize);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add((
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetInt64(6),
                reader.GetDateTime(7),
                (LogLevel)reader.GetInt32(8)
            ));
        }

        return list;
    }
}
