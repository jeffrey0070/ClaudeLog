namespace ClaudeLog.Data.Models;

public record Session(
    string SessionId,
    string Tool,
    DateTime CreatedAt,
    int Count,
    bool IsDeleted
);
