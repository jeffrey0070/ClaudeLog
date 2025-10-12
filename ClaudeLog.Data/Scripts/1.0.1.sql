-- ClaudeLog Database Schema v1.0.1

-- ========================================
-- Indexes
-- ========================================

-- Index on Sections.CreatedAt for ordering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Sections_CreatedAt' AND object_id = OBJECT_ID('dbo.Sections'))
BEGIN
    CREATE INDEX IX_Sections_CreatedAt
    ON dbo.Sections(CreatedAt DESC);

    PRINT 'Index IX_Sections_CreatedAt created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_Sections_CreatedAt already exists.';
END
GO

-- Composite index on Conversations for section grouping
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Conversations_SectionId_CreatedAt' AND object_id = OBJECT_ID('dbo.Conversations'))
BEGIN
    CREATE INDEX IX_Conversations_SectionId_CreatedAt
    ON dbo.Conversations(SectionId, CreatedAt);

    PRINT 'Index IX_Conversations_SectionId_CreatedAt created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_Conversations_SectionId_CreatedAt already exists.';
END
GO

-- Index on Conversations.Title for search performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Conversations_Title' AND object_id = OBJECT_ID('dbo.Conversations'))
BEGIN
    CREATE INDEX IX_Conversations_Title
    ON dbo.Conversations(Title);

    PRINT 'Index IX_Conversations_Title created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_Conversations_Title already exists.';
END
GO

-- Index on ErrorLogs.CreatedAt for ordering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ErrorLogs_CreatedAt' AND object_id = OBJECT_ID('dbo.ErrorLogs'))
BEGIN
    CREATE INDEX IX_ErrorLogs_CreatedAt
    ON dbo.ErrorLogs(CreatedAt DESC);

    PRINT 'Index IX_ErrorLogs_CreatedAt created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_ErrorLogs_CreatedAt already exists.';
END
GO

-- Composite index on ErrorLogs for filtering by source
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ErrorLogs_Source_CreatedAt' AND object_id = OBJECT_ID('dbo.ErrorLogs'))
BEGIN
    CREATE INDEX IX_ErrorLogs_Source_CreatedAt
    ON dbo.ErrorLogs(Source, CreatedAt DESC);

    PRINT 'Index IX_ErrorLogs_Source_CreatedAt created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_ErrorLogs_Source_CreatedAt already exists.';
END
GO

-- Index on Conversations.IsDeleted
-- Optimizes filtering by deleted/active entries
-- INCLUDE clause adds IsFavorite for covering index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Conversations_IsDeleted' AND object_id = OBJECT_ID('dbo.Conversations'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Conversations_IsDeleted
    ON dbo.Conversations(IsDeleted)
    INCLUDE (IsFavorite);

    PRINT 'Index IX_Conversations_IsDeleted created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_Conversations_IsDeleted already exists.';
END
GO