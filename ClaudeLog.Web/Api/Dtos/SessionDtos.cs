namespace ClaudeLog.Web.Api.Dtos;

public record CreateSessionRequest(
    string Tool,
    string? SessionId = null,
    DateTime? CreatedAt = null
);

public record CreateSessionResponse(string SessionId);

public record SessionDto(
    string SessionId,
    string Tool,
    DateTime CreatedAt,
    int Count,
    bool IsDeleted
);

public record UpdateSessionDeletedRequest(bool IsDeleted);
