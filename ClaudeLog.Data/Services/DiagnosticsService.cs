using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.Data.Services;

/// <summary>
/// Service for logging diagnostic information (errors, warnings, info, debug, trace).
/// Provides centralized logging for application health monitoring and debugging.
/// </summary>
public class DiagnosticsService
{
    private readonly DbContext _dbContext;
    private readonly DiagnosticsRepository _errorRepository;

    public DiagnosticsService(DbContext dbContext)
    {
        _dbContext = dbContext;
        _errorRepository = new DiagnosticsRepository(_dbContext);
    }

    /// <summary>
    /// When true, all log levels including Debug and Trace are written to database.
    /// When false, only Info, Warning, Error, and Critical are written.
    /// Default is false.
    /// </summary>
    public bool DebugEnabled { get; set; } = false;

    /// <summary>
    /// Writes diagnostic information to database with specified log level.
    /// Respects DebugEnabled setting - Debug and Trace messages are only written when DebugEnabled = true.
    /// </summary>
    /// <returns>The ID of the diagnostics entry, or null if the repository returns null or message was filtered</returns>
    public async Task<long?> WriteDiagnosticsAsync(string source, string message, LogLevel level, string? detail = null, string? path = null, string? sessionId = null, long? entryId = null, DateTime? createdAt = null)
    {
        // Filter Debug and Trace messages when DebugEnabled is false
        if (!DebugEnabled && (level == LogLevel.Debug || level == LogLevel.Trace))
        {
            return null;
        }

        return await _errorRepository.LogErrorAsync(source, message, detail, path, sessionId, entryId, createdAt, level);
    }

    public async Task<List<(long Id, string Source, string Message, string? Detail, string? Path, string? SessionId, long? EntryId, DateTime CreatedAt, LogLevel LogLevel)>> GetLogsAsync(
        LogLevel? minLevel = null,
        string? source = null,
        int page = 1,
        int pageSize = 100)
    {
        return await _errorRepository.GetLogsAsync(minLevel, source, page, pageSize);
    }
}
