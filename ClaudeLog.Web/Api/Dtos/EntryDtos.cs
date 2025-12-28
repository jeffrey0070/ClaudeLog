namespace ClaudeLog.Web.Api.Dtos;

public record CreateEntryRequest(
    string SessionId,
    string Question,
    string Response
);

public record CreateEntryResponse(long Id);

public record UpdateTitleRequest(string Title);

public record UpdateFavoriteRequest(bool IsFavorite);

public record UpdateDeletedRequest(bool IsDeleted);

public record UpdateQuestionRequest(string Question);

public record UpdateResponseRequest(string Response);

public record EntryListDto(
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

public record EntryDetailDto(
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
