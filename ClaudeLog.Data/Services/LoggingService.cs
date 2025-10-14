using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.Data.Services;

/// <summary>
/// Service for logging conversations to ClaudeLog using direct database access.
/// All application layers should use this service instead of accessing repositories directly.
/// </summary>
public class LoggingService
{
    private readonly DbContext _dbContext;
    private readonly SessionRepository _sessionRepository;
    private readonly EntryRepository _entryRepository;
    private readonly ErrorRepository _errorRepository;

    public LoggingService(DbContext dbContext)
    {
        _dbContext = dbContext;
        _sessionRepository = new SessionRepository(_dbContext);
        _entryRepository = new EntryRepository(_dbContext);
        _errorRepository = new ErrorRepository(_dbContext);
    }

    /// <summary>
    /// Creates a new session or gets an existing one, and returns its SessionId.
    /// If sessionId is not provided, generates a new GUID.
    /// </summary>
    public async Task<(bool Success, string? SessionId, string? Error)> CreateSessionAsync(string tool)
    {
        try
        {
            var sessionId = await _sessionRepository.GetOrCreateAsync(tool: tool);
            return (true, sessionId, null);
        }
        catch (Exception ex)
        {
            // Try to log error (best effort, don't throw if it fails)
            try { await LogErrorAsync("LoggingService", $"Failed to create session: {ex.Message}", ex.StackTrace ?? ""); }
            catch { /* Swallow logging errors */ }

            return (false, null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures a session exists for the given session ID (idempotent)
    /// </summary>
    public async Task<bool> EnsureSessionAsync(string sessionId, string tool = "Unknown")
    {
        try
        {
            await _sessionRepository.GetOrCreateAsync(sessionId, tool);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Logs a conversation entry (question and response pair)
    /// </summary>
    public async Task<(bool Success, long? EntryId)> LogEntryAsync(
        string sessionId,
        string question,
        string response)
    {
        try
        {
            var id = await _entryRepository.CreateAsync(sessionId, question, response);
            return (true, id);
        }
        catch (Exception ex)
        {
            await LogErrorAsync("LoggingService", $"Failed to log entry: {ex.Message}", ex.StackTrace ?? "");
            return (false, null);
        }
    }

    /// <summary>
    /// Logs a message with specified log level
    /// </summary>
    public async Task<long?> LogAsync(string source, string message, LogLevel level = LogLevel.Info, string? detail = null, string? path = null, string? sessionId = null, long? entryId = null, DateTime? createdAt = null)
    {
        try
        {
            return await _errorRepository.LogErrorAsync(source, message, detail, path, sessionId, entryId, createdAt, level);
        }
        catch
        {
            // Swallow logging failures to avoid infinite loops
            return null;
        }
    }

    /// <summary>
    /// Logs a trace-level message for detailed debugging
    /// </summary>
    public async Task<long?> LogTraceAsync(string source, string message, string? detail = null)
    {
        return await LogAsync(source, message, LogLevel.Trace, detail);
    }

    /// <summary>
    /// Logs a debug-level message for diagnostic information
    /// </summary>
    public async Task<long?> LogDebugAsync(string source, string message, string? detail = null)
    {
        return await LogAsync(source, message, LogLevel.Debug, detail);
    }

    /// <summary>
    /// Logs an info-level message for normal operation
    /// </summary>
    public async Task<long?> LogInfoAsync(string source, string message, string? detail = null)
    {
        return await LogAsync(source, message, LogLevel.Info, detail);
    }

    /// <summary>
    /// Logs a warning-level message for potentially problematic situations
    /// </summary>
    public async Task<long?> LogWarningAsync(string source, string message, string? detail = null)
    {
        return await LogAsync(source, message, LogLevel.Warning, detail);
    }

    /// <summary>
    /// Logs an error-level message
    /// </summary>
    public async Task<long?> LogErrorAsync(string source, string message, string? detail = null, string? path = null, string? sessionId = null, long? entryId = null)
    {
        return await LogAsync(source, message, LogLevel.Error, detail, path, sessionId, entryId);
    }

    /// <summary>
    /// Logs a critical-level message for severe failures
    /// </summary>
    public async Task<long?> LogCriticalAsync(string source, string message, string? detail = null, string? path = null)
    {
        return await LogAsync(source, message, LogLevel.Critical, detail, path);
    }

    /// <summary>
    /// Gets paginated list of entries with optional filtering
    /// </summary>
    public async Task<List<EntryListItem>> GetEntriesAsync(string? search = null, bool includeDeleted = false, bool showFavoritesOnly = false, int page = 1, int pageSize = 200)
    {
        return await _entryRepository.GetEntriesAsync(search, includeDeleted, showFavoritesOnly, page, pageSize);
    }

    /// <summary>
    /// Gets a single entry by ID
    /// </summary>
    public async Task<EntryDetail?> GetEntryByIdAsync(long id)
    {
        return await _entryRepository.GetEntryByIdAsync(id);
    }

    /// <summary>
    /// Updates an entry's title
    /// </summary>
    public async Task UpdateTitleAsync(long id, string title)
    {
        await _entryRepository.UpdateTitleAsync(id, title);
    }

    /// <summary>
    /// Updates an entry's favorite status
    /// </summary>
    public async Task UpdateFavoriteAsync(long id, bool isFavorite)
    {
        await _entryRepository.UpdateFavoriteAsync(id, isFavorite);
    }

    /// <summary>
    /// Updates an entry's deleted status (soft delete)
    /// </summary>
    public async Task UpdateDeletedAsync(long id, bool isDeleted)
    {
        await _entryRepository.UpdateDeletedAsync(id, isDeleted);
    }

    /// <summary>
    /// Gets paginated list of sessions
    /// </summary>
    public async Task<List<Session>> GetSessionsAsync(int days = 30, int page = 1, int pageSize = 50, bool includeDeleted = false)
    {
        return await _sessionRepository.GetSessionsAsync(days, page, pageSize, includeDeleted);
    }

    /// <summary>
    /// Updates a session's deleted status (soft delete)
    /// </summary>
    public async Task UpdateSessionDeletedAsync(string sessionId, bool isDeleted)
    {
        await _sessionRepository.UpdateDeletedAsync(sessionId, isDeleted);
    }
}
