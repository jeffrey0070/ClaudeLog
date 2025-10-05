-- Migration 001: Add IsFavorite and IsDeleted columns to Conversations table
-- Date: 2025-10-05
-- Description: Add columns to support marking conversations as favorite or deleted

USE ClaudeLog;
GO

-- Add IsFavorite column (default: 0/false)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Conversations') AND name = 'IsFavorite')
BEGIN
    ALTER TABLE dbo.Conversations
    ADD IsFavorite BIT NOT NULL DEFAULT 0;

    PRINT 'Added IsFavorite column to Conversations table';
END
ELSE
BEGIN
    PRINT 'IsFavorite column already exists';
END
GO

-- Add IsDeleted column (default: 0/false)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Conversations') AND name = 'IsDeleted')
BEGIN
    ALTER TABLE dbo.Conversations
    ADD IsDeleted BIT NOT NULL DEFAULT 0;

    PRINT 'Added IsDeleted column to Conversations table';
END
ELSE
BEGIN
    PRINT 'IsDeleted column already exists';
END
GO

-- Create index on IsDeleted for better query performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Conversations_IsDeleted' AND object_id = OBJECT_ID('dbo.Conversations'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Conversations_IsDeleted
    ON dbo.Conversations(IsDeleted)
    INCLUDE (IsFavorite);

    PRINT 'Created index IX_Conversations_IsDeleted';
END
ELSE
BEGIN
    PRINT 'Index IX_Conversations_IsDeleted already exists';
END
GO

PRINT 'Migration 001 completed successfully';
GO
