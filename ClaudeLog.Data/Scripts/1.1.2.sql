-- ========================================
-- ClaudeLog Database Schema v1.1.2
-- Replace Conversations.Id with ConversationId and add LastModifiedAt support
-- ========================================

IF COL_LENGTH('dbo.Sessions', 'LastModifiedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Sessions
    ADD LastModifiedAt DATETIME2 NULL;

    PRINT 'Column dbo.Sessions.LastModifiedAt added.';
END
ELSE
BEGIN
    PRINT 'Column dbo.Sessions.LastModifiedAt already exists.';
END
GO

UPDATE dbo.Sessions
SET LastModifiedAt = CreatedAt
WHERE LastModifiedAt IS NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Sessions')
      AND c.name = 'LastModifiedAt')
BEGIN
    ALTER TABLE dbo.Sessions
    ADD CONSTRAINT DF_Sessions_LastModifiedAt DEFAULT SYSDATETIME() FOR LastModifiedAt;

    PRINT 'Default constraint for dbo.Sessions.LastModifiedAt added.';
END
ELSE
BEGIN
    PRINT 'Default constraint for dbo.Sessions.LastModifiedAt already exists.';
END
GO

ALTER TABLE dbo.Sessions
ALTER COLUMN LastModifiedAt DATETIME2 NOT NULL;
GO

IF COL_LENGTH('dbo.Conversations', 'ConversationId') IS NULL
BEGIN
    ALTER TABLE dbo.Conversations
    ADD ConversationId UNIQUEIDENTIFIER NULL;

    PRINT 'Column dbo.Conversations.ConversationId added.';
END
ELSE
BEGIN
    PRINT 'Column dbo.Conversations.ConversationId already exists.';
END
GO

UPDATE dbo.Conversations
SET ConversationId = NEWID()
WHERE ConversationId IS NULL;
GO

IF COL_LENGTH('dbo.Conversations', 'LastModifiedAt') IS NULL
BEGIN
    ALTER TABLE dbo.Conversations
    ADD LastModifiedAt DATETIME2 NULL;

    PRINT 'Column dbo.Conversations.LastModifiedAt added.';
END
ELSE
BEGIN
    PRINT 'Column dbo.Conversations.LastModifiedAt already exists.';
END
GO

UPDATE dbo.Conversations
SET LastModifiedAt = CreatedAt
WHERE LastModifiedAt IS NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Conversations')
      AND c.name = 'ConversationId')
BEGIN
    ALTER TABLE dbo.Conversations
    ADD CONSTRAINT DF_Conversations_ConversationId DEFAULT NEWID() FOR ConversationId;

    PRINT 'Default constraint for dbo.Conversations.ConversationId added.';
END
ELSE
BEGIN
    PRINT 'Default constraint for dbo.Conversations.ConversationId already exists.';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.object_id = dc.parent_object_id
       AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('dbo.Conversations')
      AND c.name = 'LastModifiedAt')
BEGIN
    ALTER TABLE dbo.Conversations
    ADD CONSTRAINT DF_Conversations_LastModifiedAt DEFAULT SYSDATETIME() FOR LastModifiedAt;

    PRINT 'Default constraint for dbo.Conversations.LastModifiedAt added.';
END
ELSE
BEGIN
    PRINT 'Default constraint for dbo.Conversations.LastModifiedAt already exists.';
END
GO

ALTER TABLE dbo.Conversations
ALTER COLUMN ConversationId UNIQUEIDENTIFIER NOT NULL;
GO

ALTER TABLE dbo.Conversations
ALTER COLUMN LastModifiedAt DATETIME2 NOT NULL;
GO

IF COL_LENGTH('dbo.ErrorLogs', 'ConversationId') IS NULL
BEGIN
    ALTER TABLE dbo.ErrorLogs
    ADD ConversationId UNIQUEIDENTIFIER NULL;

    PRINT 'Column dbo.ErrorLogs.ConversationId added.';
END
ELSE
BEGIN
    PRINT 'Column dbo.ErrorLogs.ConversationId already exists.';
END
GO

IF COL_LENGTH('dbo.ErrorLogs', 'EntryId') IS NOT NULL
BEGIN
    EXEC('
        UPDATE e
        SET ConversationId = c.ConversationId
        FROM dbo.ErrorLogs e
        INNER JOIN dbo.Conversations c
            ON c.Id = e.EntryId
        WHERE e.EntryId IS NOT NULL
          AND e.ConversationId IS NULL;
    ');

    PRINT 'dbo.ErrorLogs.ConversationId backfilled from dbo.ErrorLogs.EntryId.';
END
ELSE
BEGIN
    PRINT 'dbo.ErrorLogs.EntryId already removed.';
END
GO

IF EXISTS (
    SELECT ConversationId
    FROM dbo.Conversations
    GROUP BY ConversationId
    HAVING COUNT(*) > 1)
BEGIN
    RAISERROR ('Duplicate ConversationId values found in dbo.Conversations.', 16, 1);
END
GO

DECLARE @ConversationsPrimaryKeyName sysname;

SELECT @ConversationsPrimaryKeyName = kc.name
FROM sys.key_constraints kc
INNER JOIN sys.tables t
    ON t.object_id = kc.parent_object_id
WHERE kc.type = 'PK'
  AND t.name = 'Conversations';

IF @ConversationsPrimaryKeyName IS NOT NULL
BEGIN
    DECLARE @CurrentPrimaryKeyColumn sysname;

    SELECT TOP (1) @CurrentPrimaryKeyColumn = c.name
    FROM sys.key_constraints kc
    INNER JOIN sys.index_columns ic
        ON ic.object_id = kc.parent_object_id
       AND ic.index_id = kc.unique_index_id
    INNER JOIN sys.columns c
        ON c.object_id = ic.object_id
       AND c.column_id = ic.column_id
    WHERE kc.name = @ConversationsPrimaryKeyName
    ORDER BY ic.key_ordinal;

    IF @CurrentPrimaryKeyColumn <> 'ConversationId'
    BEGIN
        EXEC('ALTER TABLE dbo.Conversations DROP CONSTRAINT [' + @ConversationsPrimaryKeyName + ']');
        PRINT 'Old primary key dropped from dbo.Conversations.';
    END
    ELSE
    BEGIN
        PRINT 'dbo.Conversations already uses ConversationId as primary key.';
    END
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.key_constraints kc
    INNER JOIN sys.tables t
        ON t.object_id = kc.parent_object_id
    INNER JOIN sys.index_columns ic
        ON ic.object_id = kc.parent_object_id
       AND ic.index_id = kc.unique_index_id
    INNER JOIN sys.columns c
        ON c.object_id = ic.object_id
       AND c.column_id = ic.column_id
    WHERE kc.type = 'PK'
      AND t.name = 'Conversations'
      AND c.name = 'ConversationId')
BEGIN
    ALTER TABLE dbo.Conversations
    ADD CONSTRAINT PK_Conversations_ConversationId PRIMARY KEY CLUSTERED (ConversationId);

    PRINT 'Primary key PK_Conversations_ConversationId created.';
END
ELSE
BEGIN
    PRINT 'Primary key on dbo.Conversations.ConversationId already exists.';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ErrorLogs_ConversationId_CreatedAt'
      AND object_id = OBJECT_ID('dbo.ErrorLogs'))
BEGIN
    BEGIN TRY
        CREATE INDEX IX_ErrorLogs_ConversationId_CreatedAt
        ON dbo.ErrorLogs(ConversationId, CreatedAt DESC);

        PRINT 'Index IX_ErrorLogs_ConversationId_CreatedAt created.';
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() IN (1913, 2714)
        BEGIN
            PRINT 'Index IX_ErrorLogs_ConversationId_CreatedAt already exists.';
        END
        ELSE
        BEGIN
            DECLARE @ErrorMessage NVARCHAR(4000);
            DECLARE @ErrorSeverity INT;
            DECLARE @ErrorState INT;

            SELECT
                @ErrorMessage = ERROR_MESSAGE(),
                @ErrorSeverity = ERROR_SEVERITY(),
                @ErrorState = ERROR_STATE();

            RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
        END
    END CATCH
END
ELSE
BEGIN
    PRINT 'Index IX_ErrorLogs_ConversationId_CreatedAt already exists.';
END
GO

IF COL_LENGTH('dbo.Conversations', 'Id') IS NOT NULL
BEGIN
    ALTER TABLE dbo.Conversations
    DROP COLUMN Id;

    PRINT 'Column dbo.Conversations.Id dropped.';
END
ELSE
BEGIN
    PRINT 'Column dbo.Conversations.Id already removed.';
END
GO

IF COL_LENGTH('dbo.ErrorLogs', 'EntryId') IS NOT NULL
BEGIN
    DECLARE @UnmappedLegacyEntryReferences int;
    SET @UnmappedLegacyEntryReferences = 0;

    EXEC sp_executesql
        N'
            SELECT @Count = COUNT(*)
            FROM dbo.ErrorLogs
            WHERE EntryId IS NOT NULL
              AND ConversationId IS NULL;
        ',
        N'@Count int OUTPUT',
        @Count = @UnmappedLegacyEntryReferences OUTPUT;

    IF @UnmappedLegacyEntryReferences > 0
    BEGIN
        RAISERROR ('Cannot drop dbo.ErrorLogs.EntryId because some rows could not be mapped to ConversationId.', 16, 1);
    END

    ALTER TABLE dbo.ErrorLogs
    DROP COLUMN EntryId;

    PRINT 'Column dbo.ErrorLogs.EntryId dropped.';
END
ELSE
BEGIN
    PRINT 'Column dbo.ErrorLogs.EntryId already removed.';
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Sessions_SetLastModifiedAt
ON dbo.Sessions
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF SESSION_CONTEXT(N'ClaudeLogPreserveLastModifiedAt') = CAST(1 AS sql_variant)
        RETURN;

    IF TRIGGER_NESTLEVEL() > 1
        RETURN;

    UPDATE s
    SET LastModifiedAt = SYSDATETIME()
    FROM dbo.Sessions s
    INNER JOIN inserted i
        ON i.SessionId = s.SessionId;
END
GO

CREATE OR ALTER TRIGGER dbo.trg_Conversations_SetLastModifiedAt
ON dbo.Conversations
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF SESSION_CONTEXT(N'ClaudeLogPreserveLastModifiedAt') = CAST(1 AS sql_variant)
        RETURN;

    IF TRIGGER_NESTLEVEL() > 1
        RETURN;

    UPDATE c
    SET LastModifiedAt = SYSDATETIME()
    FROM dbo.Conversations c
    INNER JOIN inserted i
        ON i.ConversationId = c.ConversationId;
END
GO
