namespace ClaudeLog.Web.Api.Dtos;

public record CreateEntryRequest(
    string SectionId,
    string Question,
    string Response
);

public record CreateEntryResponse(long Id);

public record UpdateTitleRequest(string Title);

public record UpdateFavoriteRequest(bool IsFavorite);

public record UpdateDeletedRequest(bool IsDeleted);

public record EntryListDto(
    long Id,
    string Title,
    DateTime CreatedAt,
    Guid SectionId,
    DateTime SectionCreatedAt,
    string Tool,
    bool IsFavorite,
    bool IsDeleted,
    bool SectionIsDeleted
);

public record EntryDetailDto(
    long Id,
    string Title,
    string Question,
    string Response,
    DateTime CreatedAt,
    Guid SectionId,
    string Tool,
    DateTime SectionCreatedAt,
    bool IsFavorite,
    bool IsDeleted
);
