namespace ClaudeLog.Data.Models;

public record CreateSectionRequest(
    string Tool,
    string? SectionId = null,
    DateTime? CreatedAt = null
);

public record CreateSectionResponse(string SectionId);

public record Section(
    Guid SectionId,
    string Tool,
    DateTime CreatedAt,
    int Count
);
