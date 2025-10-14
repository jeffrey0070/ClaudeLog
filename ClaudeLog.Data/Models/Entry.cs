namespace ClaudeLog.Data.Models;

public record EntryListItem(
    long Id,
    string Title,
    DateTime CreatedAt,
    string SessionId,
    DateTime SessionCreatedAt,
    string Tool,
    bool IsFavorite,
    bool IsDeleted,
    bool SessionIsDeleted
);

public record EntryDetail(
    long Id,
    string Title,
    string Question,
    string Response,
    DateTime CreatedAt,
    string SessionId,
    string Tool,
    DateTime SessionCreatedAt,
    bool IsFavorite,
    bool IsDeleted
);
