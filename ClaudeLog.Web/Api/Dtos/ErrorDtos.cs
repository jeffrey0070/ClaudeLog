namespace ClaudeLog.Web.Api.Dtos;

public record LogErrorRequest(
    string Source,
    string Message,
    string? Detail = null,
    string? Path = null,
    string? SessionId = null,
    long? EntryId = null,
    DateTime? CreatedAt = null
);

public record LogErrorResponse(bool Ok, long Id);

public record ErrorLogDto(
    long Id,
    string Source,
    string Message,
    string? Detail,
    string? Path,
    string? SessionId,
    long? EntryId,
    DateTime CreatedAt,
    ClaudeLog.Data.Models.LogLevel LogLevel);
