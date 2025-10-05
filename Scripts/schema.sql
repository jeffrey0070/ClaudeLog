-- ClaudeLog Database Schema
-- Database: ClaudeLog
-- SQL Server with Windows Integrated Security

USE ClaudeLog;
GO

-- Create Sections table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
BEGIN
    CREATE TABLE dbo.Sections (
        SectionId UNIQUEIDENTIFIER PRIMARY KEY,
        Tool NVARCHAR(32) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );

    PRINT 'Table dbo.Sections created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.Sections already exists.';
END
GO

-- Create Conversations table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Conversations')
BEGIN
    CREATE TABLE dbo.Conversations (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        SectionId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(400) NOT NULL,
        Question NVARCHAR(MAX) NOT NULL,
        Response NVARCHAR(MAX) NOT NULL,
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

-- Create ErrorLogs table
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

PRINT 'Schema creation completed successfully.';
