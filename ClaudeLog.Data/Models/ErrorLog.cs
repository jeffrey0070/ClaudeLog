namespace ClaudeLog.Data.Models;

public record ErrorLog(
    long Id,
    string Source,
    string Message,
    string? Detail,
    string? Path,
    string? SessionId,
    long? EntryId,
    DateTime CreatedAt,
    LogLevel LogLevel
);
