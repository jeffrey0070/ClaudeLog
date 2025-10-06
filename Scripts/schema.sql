-- ========================================
-- ClaudeLog Database Schema
-- ========================================
-- Database: ClaudeLog
-- SQL Server with Windows Integrated Security
-- Creates tables for storing Claude Code conversation logs
--
-- IMPORTANT: This script is for creating tables from scratch.
-- If you need to change the schema, simply modify this file.
-- DO NOT add upgrade/migration logic here.
-- For upgrades to existing databases, create separate migration scripts.
-- ========================================

USE ClaudeLog;
GO

-- ========================================
-- Sections Table
-- ========================================
-- Represents a CLI session (conversation group)
-- Each session has a unique GUID provided by Claude Code
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
BEGIN
    CREATE TABLE dbo.Sections (
        SectionId UNIQUEIDENTIFIER PRIMARY KEY,  -- Session GUID from Claude Code
        Tool NVARCHAR(32) NOT NULL,              -- CLI tool name (e.g., "Claude Code")
        IsDeleted BIT NOT NULL DEFAULT 0,        -- Soft delete (can be restored)
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()  -- Local timestamp
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
-- Stores individual Q&A pairs from conversations
-- Title is auto-generated from question text
-- IsFavorite and IsDeleted support UI filtering
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Conversations')
BEGIN
    CREATE TABLE dbo.Conversations (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        SectionId UNIQUEIDENTIFIER NOT NULL,         -- Links to Sections table
        Title NVARCHAR(400) NOT NULL,                -- Auto-generated title (200 chars, Unicode safe)
        Question NVARCHAR(MAX) NOT NULL,             -- User question (trimmed)
        Response NVARCHAR(MAX) NOT NULL,             -- Assistant response (trimmed)
        IsFavorite BIT NOT NULL DEFAULT 0,           -- User can mark as favorite
        IsDeleted BIT NOT NULL DEFAULT 0,            -- Soft delete (can be restored)
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),  -- Local timestamp
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
-- Stores errors from web app and hook for diagnostics
-- Helps troubleshoot integration issues
-- ========================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ErrorLogs')
BEGIN
    CREATE TABLE dbo.ErrorLogs (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        Source NVARCHAR(64) NOT NULL,            -- Source component (e.g., "Hook.Claude", "UI")
        Message NVARCHAR(1024) NOT NULL,         -- Error message
        Detail NVARCHAR(MAX) NULL,               -- Stack trace or additional details
        Path NVARCHAR(256) NULL,                 -- File path if relevant
        SectionId UNIQUEIDENTIFIER NULL,         -- Related section if applicable
        EntryId BIGINT NULL,                     -- Related entry if applicable
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME()  -- Local timestamp
    );

    PRINT 'Table dbo.ErrorLogs created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.ErrorLogs already exists.';
END
GO

PRINT 'Schema creation completed successfully.';
