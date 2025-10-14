-- ========================================
-- ClaudeLog Database Schema v1.0.0
-- Initial schema with all tables and indexes
-- ========================================

-- ========================================
-- DatabaseVersion Table
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DatabaseVersion')
BEGIN
    CREATE TABLE dbo.DatabaseVersion (
        Version NVARCHAR(32) NOT NULL PRIMARY KEY,
        AppliedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );

    PRINT 'Table dbo.DatabaseVersion created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.DatabaseVersion already exists.';
END
GO

-- ========================================
-- Sessions Table
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sessions')
BEGIN
    CREATE TABLE dbo.Sessions (
        SessionId NVARCHAR(128) PRIMARY KEY,
        Tool NVARCHAR(32) NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );

    -- Index on Sessions.CreatedAt for ordering
    CREATE INDEX IX_Sessions_CreatedAt
    ON dbo.Sessions(CreatedAt DESC);

    PRINT 'Table dbo.Sessions created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.Sessions already exists.';
END
GO

-- ========================================
-- Conversations Table
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Conversations')
BEGIN
    CREATE TABLE dbo.Conversations (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        SessionId NVARCHAR(128) NOT NULL,
        Title NVARCHAR(400) NOT NULL,
        Question NVARCHAR(MAX) NOT NULL,
        Response NVARCHAR(MAX) NOT NULL,
        IsFavorite BIT NOT NULL DEFAULT 0,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        CONSTRAINT FK_Conversations_Sessions FOREIGN KEY (SessionId)
            REFERENCES dbo.Sessions(SessionId)
    );

    -- Composite index on Conversations for session grouping
    CREATE INDEX IX_Conversations_SessionId_CreatedAt
    ON dbo.Conversations(SessionId, CreatedAt);

    -- Index on Conversations.Title for search performance
    CREATE INDEX IX_Conversations_Title
    ON dbo.Conversations(Title);

    -- Index on Conversations.IsDeleted with covering index
    CREATE NONCLUSTERED INDEX IX_Conversations_IsDeleted
    ON dbo.Conversations(IsDeleted)
    INCLUDE (IsFavorite);

    PRINT 'Table dbo.Conversations created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.Conversations already exists.';
END
GO

-- ========================================
-- ErrorLogs Table
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ErrorLogs')
BEGIN
    CREATE TABLE dbo.ErrorLogs (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Source NVARCHAR(64) NOT NULL,
        Message NVARCHAR(1024) NOT NULL,
        Detail NVARCHAR(MAX) NULL,
        Path NVARCHAR(256) NULL,
        SessionId NVARCHAR(128) NULL,
        EntryId BIGINT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );

    -- Index on ErrorLogs.CreatedAt for ordering
    CREATE INDEX IX_ErrorLogs_CreatedAt
    ON dbo.ErrorLogs(CreatedAt DESC);

    -- Composite index on ErrorLogs for filtering by source
    CREATE INDEX IX_ErrorLogs_Source_CreatedAt
    ON dbo.ErrorLogs(Source, CreatedAt DESC);

    PRINT 'Table dbo.ErrorLogs created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.ErrorLogs already exists.';
END
GO
