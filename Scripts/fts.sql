-- ClaudeLog Full-Text Search (Optional - Phase 3)
-- Database: ClaudeLog
-- Requires Full-Text Search feature to be installed on SQL Server

USE ClaudeLog;
GO

-- Note: This script is for Phase 3 implementation
-- Run this only when you're ready to implement full-text search

-- Create Full-Text Catalog
IF NOT EXISTS (SELECT * FROM sys.fulltext_catalogs WHERE name = 'ClaudeLogCatalog')
BEGIN
    CREATE FULLTEXT CATALOG ClaudeLogCatalog AS DEFAULT;
    PRINT 'Full-Text Catalog ClaudeLogCatalog created successfully.';
END
ELSE
BEGIN
    PRINT 'Full-Text Catalog ClaudeLogCatalog already exists.';
END
GO

-- Create Full-Text Index on Conversations table
-- Supports Chinese word breaker (LCID 2052 for Simplified Chinese)
IF NOT EXISTS (SELECT * FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID('dbo.Conversations'))
BEGIN
    CREATE FULLTEXT INDEX ON dbo.Conversations(
        Title LANGUAGE 2052,      -- Simplified Chinese
        Question LANGUAGE 2052,
        Response LANGUAGE 2052
    )
    KEY INDEX PK__Conversations  -- Replace with actual PK name if different
    ON ClaudeLogCatalog
    WITH CHANGE_TRACKING AUTO;

    PRINT 'Full-Text Index on dbo.Conversations created successfully.';
END
ELSE
BEGIN
    PRINT 'Full-Text Index on dbo.Conversations already exists.';
END
GO

PRINT 'Full-Text Search setup completed successfully.';
PRINT 'Note: Use CONTAINS or FREETEXT in queries for full-text search.';
