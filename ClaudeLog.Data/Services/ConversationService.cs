using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.Data.Services;

/// <summary>
/// Service for managing conversation sessions and entries.
/// Handles session creation, entry writting, and conversation retrieval/management.
/// </summary>
public class ConversationService
{
    private readonly DbContext _dbContext;
    private readonly SessionRepository _sessionRepository;
    private readonly EntryRepository _entryRepository;

    public ConversationService(DbContext dbContext)
    {
        _dbContext = dbContext;
        _sessionRepository = new SessionRepository(_dbContext);
        _entryRepository = new EntryRepository(_dbContext);
    }

    #region Session Operations

    /// <summary>
    /// Creates a new session or gets an existing one, and returns its SessionId.
    /// If sessionId is not provided, generates a new GUID.
    /// </summary>
    /// <returns>The session ID</returns>
    public async Task<string> CreateSessionAsync(string tool)
    {
        return await _sessionRepository.GetOrCreateAsync(tool: tool);
    }

    /// <summary>
    /// Ensures a session exists for the given session ID (idempotent)
    /// </summary>
    public async Task EnsureSessionAsync(string sessionId, string tool = "Unknown")
    {
        await _sessionRepository.GetOrCreateAsync(sessionId, tool);
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

    #endregion

    #region Entry Operations

    /// <summary>
    /// Writes a conversation entry (question and response pair)
    /// </summary>
    /// <returns>The ID of the newly created entry</returns>
    /// <exception cref="ArgumentException">Thrown if sessionId, question, or response is null/empty</exception>
    public async Task<long> WriteEntryAsync(
        string sessionId,
        string question,
        string response)
    {
        return await _entryRepository.CreateAsync(sessionId, question, response);
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

    #endregion
}
