namespace ClaudeLog.Web.Data;

public static class Queries
{
    // Sections
    public const string InsertSection = @"
        INSERT INTO dbo.Sections (SectionId, Tool, CreatedAt)
        VALUES (@SectionId, @Tool, @CreatedAt)";

    public const string GetSections = @"
        SELECT s.SectionId, s.Tool, s.CreatedAt, COUNT(c.Id) as Count
        FROM dbo.Sections s
        LEFT JOIN dbo.Conversations c ON s.SectionId = c.SectionId
        WHERE s.CreatedAt >= DATEADD(DAY, -@Days, SYSDATETIME())
        GROUP BY s.SectionId, s.Tool, s.CreatedAt
        ORDER BY s.CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

    // Conversations
    public const string InsertConversation = @"
        INSERT INTO dbo.Conversations (SectionId, Title, Question, Response, CreatedAt)
        OUTPUT INSERTED.Id
        VALUES (@SectionId, @Title, @Question, @Response, @CreatedAt)";

    public const string GetConversations = @"
        SELECT c.Id, c.Title, c.CreatedAt, c.SectionId, s.CreatedAt as SectionCreatedAt, s.Tool,
               c.IsFavorite, c.IsDeleted
        FROM dbo.Conversations c
        INNER JOIN dbo.Sections s ON c.SectionId = s.SectionId
        WHERE (@Search IS NULL OR @Search = '' OR
               c.Title LIKE @SearchPattern OR
               c.Question LIKE @SearchPattern OR
               c.Response LIKE @SearchPattern)
          AND (@IncludeDeleted = 1 OR c.IsDeleted = 0)
          AND (@ShowFavoritesOnly = 0 OR c.IsFavorite = 1)
        ORDER BY s.CreatedAt DESC, c.CreatedAt DESC
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

    public const string GetConversationById = @"
        SELECT c.Id, c.Title, c.Question, c.Response, c.CreatedAt,
               c.SectionId, s.Tool, s.CreatedAt as SectionCreatedAt,
               c.IsFavorite, c.IsDeleted
        FROM dbo.Conversations c
        INNER JOIN dbo.Sections s ON c.SectionId = s.SectionId
        WHERE c.Id = @Id";

    public const string UpdateConversationTitle = @"
        UPDATE dbo.Conversations
        SET Title = @Title
        WHERE Id = @Id";

    public const string UpdateConversationFavorite = @"
        UPDATE dbo.Conversations
        SET IsFavorite = @IsFavorite
        WHERE Id = @Id";

    public const string UpdateConversationDeleted = @"
        UPDATE dbo.Conversations
        SET IsDeleted = @IsDeleted
        WHERE Id = @Id";

    public const string GetConversationsCount = @"
        SELECT COUNT(*)
        FROM dbo.Conversations c
        INNER JOIN dbo.Sections s ON c.SectionId = s.SectionId
        WHERE (@Search IS NULL OR @Search = '' OR
               c.Title LIKE @SearchPattern OR
               c.Question LIKE @SearchPattern OR
               c.Response LIKE @SearchPattern)";
}
