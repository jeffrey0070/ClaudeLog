-- ========================================
-- ClaudeLog Database Schema v1.1.1
-- Seed predefined "Knowledge Base" session
-- ========================================

IF NOT EXISTS (SELECT * FROM dbo.Sessions WHERE SessionId = '00000000-0000-0000-0000-000000000001')
BEGIN
    INSERT INTO dbo.Sessions (SessionId, Tool, CreatedAt)
    VALUES ('00000000-0000-0000-0000-000000000001', 'Knowledge Base', '9999-12-31 23:59:59.9999999');

    PRINT 'Predefined session "Knowledge Base" inserted.';
END
ELSE
BEGIN
    PRINT 'Predefined session "Knowledge Base" already exists.';
END
GO