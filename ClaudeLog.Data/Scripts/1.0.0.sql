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
-- Sections Table
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
BEGIN
    CREATE TABLE dbo.Sections (
        SectionId UNIQUEIDENTIFIER PRIMARY KEY,
        Tool NVARCHAR(32) NOT NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );

    PRINT 'Table dbo.Sections created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.Sections already exists.';
END
GO

-- ========================================
-- Conversations Table
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Conversations')
BEGIN
    CREATE TABLE dbo.Conversations (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        SectionId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(400) NOT NULL,
        Question NVARCHAR(MAX) NOT NULL,
        Response NVARCHAR(MAX) NOT NULL,
        IsFavorite BIT NOT NULL DEFAULT 0,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        CONSTRAINT FK_Conversations_Sections FOREIGN KEY (SectionId)
            REFERENCES dbo.Sections(SectionId)
    );

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
        SectionId UNIQUEIDENTIFIER NULL,
        EntryId BIGINT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );

    PRINT 'Table dbo.ErrorLogs created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.ErrorLogs already exists.';
END
GO
