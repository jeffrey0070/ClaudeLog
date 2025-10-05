namespace ClaudeLog.Web.Api.Dtos;

public record LogErrorRequest(
    string Source,
    string Message,
    string? Detail = null,
    string? Path = null,
    string? SectionId = null,
    long? EntryId = null,
    DateTime? CreatedAt = null
);

public record LogErrorResponse(bool Ok, long Id);
