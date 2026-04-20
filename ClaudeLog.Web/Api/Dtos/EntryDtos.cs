namespace ClaudeLog.Web.Api.Dtos;

public record CreateEntryRequest(
    string SessionId,
    string Question,
    string Response
);

public record CreateEntryResponse(Guid ConversationId);

public record UpdateTitleRequest(string Title);

public record UpdateFavoriteRequest(bool IsFavorite);

public record UpdateDeletedRequest(bool IsDeleted);

public record UpdateQuestionRequest(string Question);

public record UpdateResponseRequest(string Response);

public record UpdateSessionIdRequest(string SessionId);

public record EntryListDto(
    Guid ConversationId,
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
    Guid ConversationId,
    string Title,
    string Question,
    string QuestionHtml,
    string Response,
    string ResponseHtml,
    DateTime CreatedAt,
    string SessionId,
    string Tool,
    DateTime SessionCreatedAt,
    bool IsFavorite,
    bool IsDeleted
);
