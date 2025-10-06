using ClaudeLog.Data.Models;
using ClaudeLog.Data.Repositories;

namespace ClaudeLog.Web.Services;

public class ErrorLogger
{
    private readonly ErrorRepository _errorRepository;

    public ErrorLogger(ErrorRepository errorRepository)
    {
        _errorRepository = errorRepository;
    }

    public async Task<long?> LogErrorAsync(
        string source,
        string message,
        string? detail = null,
        string? path = null,
        Guid? sectionId = null,
        long? entryId = null)
    {
        try
        {
            var request = new LogErrorRequest(
                source,
                message,
                detail,
                path,
                sectionId?.ToString(),
                entryId,
                DateTime.Now);

            var response = await _errorRepository.LogErrorAsync(request);
            return response.Id;
        }
        catch
        {
            // Swallow errors in error logger to prevent infinite loops
            return null;
        }
    }

    public async Task LogExceptionAsync(
        string source,
        Exception ex,
        string? path = null,
        Guid? sectionId = null,
        long? entryId = null)
    {
        await LogErrorAsync(
            source,
            ex.Message,
            ex.StackTrace,
            path,
            sectionId,
            entryId);
    }
}
