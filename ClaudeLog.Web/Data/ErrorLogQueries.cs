namespace ClaudeLog.Web.Data;

public static class ErrorLogQueries
{
    public const string InsertError = @"
        INSERT INTO dbo.ErrorLogs (Source, Message, Detail, Path, SectionId, EntryId, CreatedAt)
        OUTPUT INSERTED.Id
        VALUES (@Source, @Message, @Detail, @Path, @SectionId, @EntryId, @CreatedAt)";

    public const string GetErrors = @"
        SELECT Id, Source, Message, Detail, Path, SectionId, EntryId, CreatedAt
        FROM dbo.ErrorLogs
        WHERE (@Source IS NULL OR Source = @Source)
        ORDER BY CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
}
