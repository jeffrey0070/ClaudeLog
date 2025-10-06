using ClaudeLog.Data;
using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.MCP;

/// <summary>
/// Service for logging conversations to ClaudeLog using direct database access
/// </summary>
public class LoggingService
{
    private readonly DbContext _dbContext;
    private readonly SectionRepository _sectionRepository;
    private readonly EntryRepository _entryRepository;
    private readonly ErrorRepository _errorRepository;

    public LoggingService()
    {
        _dbContext = new DbContext();
        _sectionRepository = new SectionRepository(_dbContext);
        _entryRepository = new EntryRepository(_dbContext);
        _errorRepository = new ErrorRepository(_dbContext);
    }

    /// <summary>
    /// Creates a new section and returns its generated SectionId.
    /// The server generates a new GUID for SectionId and uses current time for CreatedAt.
    /// </summary>
    public async Task<(bool Success, string? SectionId, string? Error)> CreateSectionAsync(string tool)
    {
        try
        {
            var request = new CreateSectionRequest(tool, null, null);
            var response = await _sectionRepository.CreateAsync(request);
            return (true, response.SectionId, null);
        }
        catch (Exception ex)
        {
            // Try to log error (best effort, don't throw if it fails)
            try { await LogErrorAsync("MCP.Server", $"Failed to create section: {ex.Message}", ex.StackTrace ?? ""); }
            catch { /* Swallow logging errors */ }

            return (false, null, $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Ensures a section exists for the given session ID
    /// </summary>
    public async Task<bool> EnsureSectionAsync(string sessionId, string tool = "Codex")
    {
        try
        {
            var request = new CreateSectionRequest(tool, sessionId, null);
            await _sectionRepository.CreateAsync(request);
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
            var request = new CreateEntryRequest(sessionId, question, response);
            var result = await _entryRepository.CreateAsync(request);
            return (true, result.Id);
        }
        catch (Exception ex)
        {
            await LogErrorAsync("MCP.Server", $"Failed to log entry: {ex.Message}", ex.StackTrace ?? "");
            return (false, null);
        }
    }

    /// <summary>
    /// Logs an error to ClaudeLog
    /// </summary>
    private async Task LogErrorAsync(string source, string message, string detail)
    {
        try
        {
            var request = new LogErrorRequest(source, message, detail, null, null, null, null);
            await _errorRepository.LogErrorAsync(request);
        }
        catch
        {
            // Swallow error logging failures to avoid infinite loops
        }
    }
}
