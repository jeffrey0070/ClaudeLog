namespace ClaudeLog.Data.Models;

public class CoreDataExportFile
{
    public int FormatVersion { get; init; }
    public DateTime ExportedAt { get; init; }
    public List<CoreDataSessionRecord> Sessions { get; init; } = [];
}

public class CoreDataSessionRecord
{
    public string SessionId { get; init; } = string.Empty;
    public string Tool { get; init; } = string.Empty;
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public List<CoreDataConversationRecord> Conversations { get; init; } = [];
}

public class CoreDataConversationRecord
{
    public Guid ConversationId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public string Response { get; init; } = string.Empty;
    public bool IsFavorite { get; init; }
    public bool IsDeleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastModifiedAt { get; init; }
}

public class CoreDataImportSummary
{
    public int SessionsInserted { get; set; }
    public int SessionsUpdated { get; set; }
    public int SessionsSkipped { get; set; }
    public int ConversationsInserted { get; set; }
    public int ConversationsUpdated { get; set; }
    public int ConversationsSkipped { get; set; }
    public List<string> Errors { get; init; } = [];
}
