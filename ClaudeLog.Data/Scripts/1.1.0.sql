-- ========================================
-- ClaudeLog Database Schema v1.1.0
-- Add LogLevel support to ErrorLogs table
-- ========================================

-- Add LogLevel column to ErrorLogs table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ErrorLogs') AND name = 'LogLevel')
BEGIN
    ALTER TABLE dbo.ErrorLogs
    ADD LogLevel INT NOT NULL DEFAULT 4; -- Default to Error level

    PRINT 'Column LogLevel added to dbo.ErrorLogs.';
END
ELSE
BEGIN
    PRINT 'Column LogLevel already exists in dbo.ErrorLogs.';
END
GO

-- Create index on LogLevel for filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ErrorLogs_LogLevel_CreatedAt' AND object_id = OBJECT_ID('dbo.ErrorLogs'))
BEGIN
    CREATE INDEX IX_ErrorLogs_LogLevel_CreatedAt
    ON dbo.ErrorLogs(LogLevel, CreatedAt DESC);

    PRINT 'Index IX_ErrorLogs_LogLevel_CreatedAt created successfully.';
END
ELSE
BEGIN
    PRINT 'Index IX_ErrorLogs_LogLevel_CreatedAt already exists.';
END
GO
