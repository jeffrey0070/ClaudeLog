namespace ClaudeLog.Web.Api.Dtos;

public record CreateSectionRequest(
    string Tool,
    string? SectionId = null,
    DateTime? CreatedAt = null
);

public record CreateSectionResponse(string SectionId);

public record SectionDto(
    Guid SectionId,
    string Tool,
    DateTime CreatedAt,
    int Count
);
