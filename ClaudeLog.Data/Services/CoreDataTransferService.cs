using System.Text.Json;
using ClaudeLog.Data.Models;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data.Services;

/// <summary>
/// Handles export/import of core data for sessions and conversations.
/// Keeps transfer logic isolated from the interactive repositories.
/// </summary>
public class CoreDataTransferService
{
    private const int CurrentFormatVersion = 1;
    private readonly DbContext _dbContext;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public CoreDataTransferService(DbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<CoreDataExportFile> ExportAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync(cancellationToken);

        var export = new CoreDataExportFile
        {
            FormatVersion = CurrentFormatVersion,
            ExportedAt = DateTime.Now
        };

        var sessionsById = new Dictionary<string, CoreDataSessionRecord>(StringComparer.OrdinalIgnoreCase);

        const string sessionsQuery = """
            SELECT SessionId, Tool, IsDeleted, CreatedAt, LastModifiedAt
            FROM dbo.Sessions
            WHERE IsDeleted = 0
            ORDER BY CreatedAt ASC
            """;

        using (var sessionsCommand = new SqlCommand(sessionsQuery, conn))
        using (var reader = await sessionsCommand.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var session = new CoreDataSessionRecord
                {
                    SessionId = reader.GetString(0),
                    Tool = reader.GetString(1),
                    IsDeleted = reader.GetBoolean(2),
                    CreatedAt = reader.GetDateTime(3),
                    LastModifiedAt = reader.GetDateTime(4),
                    Conversations = []
                };

                sessionsById[session.SessionId] = session;
                export.Sessions.Add(session);
            }
        }

        const string conversationsQuery = """
            SELECT ConversationId, SessionId, Title, Question, Response, IsFavorite, IsDeleted, CreatedAt, LastModifiedAt
            FROM dbo.Conversations
            WHERE IsDeleted = 0
            ORDER BY SessionId ASC, CreatedAt ASC
            """;

        using var conversationsCommand = new SqlCommand(conversationsQuery, conn);
        using var conversationsReader = await conversationsCommand.ExecuteReaderAsync(cancellationToken);
        while (await conversationsReader.ReadAsync(cancellationToken))
        {
            var sessionId = conversationsReader.GetString(1);
            if (!sessionsById.TryGetValue(sessionId, out var session))
            {
                continue;
            }

            session.Conversations.Add(new CoreDataConversationRecord
            {
                ConversationId = conversationsReader.GetGuid(0),
                Title = conversationsReader.GetString(2),
                Question = conversationsReader.GetString(3),
                Response = conversationsReader.GetString(4),
                IsFavorite = conversationsReader.GetBoolean(5),
                IsDeleted = conversationsReader.GetBoolean(6),
                CreatedAt = conversationsReader.GetDateTime(7),
                LastModifiedAt = conversationsReader.GetDateTime(8)
            });
        }

        return export;
    }

    public async Task<string> ExportJsonAsync(CancellationToken cancellationToken = default)
    {
        var export = await ExportAsync(cancellationToken);
        return JsonSerializer.Serialize(export, _jsonOptions);
    }

    public async Task<CoreDataImportSummary> ImportAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var importFile = await JsonSerializer.DeserializeAsync<CoreDataExportFile>(stream, _jsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Import file could not be parsed.");

        ValidateImportFile(importFile);

        var summary = new CoreDataImportSummary();

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync(cancellationToken);
        using var transaction = conn.BeginTransaction();

        try
        {
            await SetPreserveLastModifiedAtAsync(conn, transaction, true, cancellationToken);

            foreach (var session in importFile.Sessions)
            {
                await UpsertSessionAsync(conn, transaction, session, summary, cancellationToken);
            }

            foreach (var session in importFile.Sessions)
            {
                foreach (var conversation in session.Conversations)
                {
                    await UpsertConversationAsync(conn, transaction, session.SessionId, conversation, summary, cancellationToken);
                }
            }

            await SetPreserveLastModifiedAtAsync(conn, transaction, false, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            try
            {
                await SetPreserveLastModifiedAtAsync(conn, transaction, false, cancellationToken);
            }
            catch
            {
                // Preserve the original failure and roll back below.
            }

            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return summary;
    }

    private static void ValidateImportFile(CoreDataExportFile importFile)
    {
        if (importFile.FormatVersion != CurrentFormatVersion)
        {
            throw new InvalidOperationException($"Unsupported import format version {importFile.FormatVersion}.");
        }

        var sessionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conversationIds = new HashSet<Guid>();

        foreach (var session in importFile.Sessions)
        {
            if (string.IsNullOrWhiteSpace(session.SessionId))
            {
                throw new InvalidOperationException("Import file contains a session with an empty sessionId.");
            }

            if (!sessionIds.Add(session.SessionId))
            {
                throw new InvalidOperationException($"Import file contains duplicate sessionId '{session.SessionId}'.");
            }

            foreach (var conversation in session.Conversations)
            {
                if (conversation.ConversationId == Guid.Empty)
                {
                    throw new InvalidOperationException($"Session '{session.SessionId}' contains a conversation with an empty conversationId.");
                }

                if (!conversationIds.Add(conversation.ConversationId))
                {
                    throw new InvalidOperationException($"Import file contains duplicate conversationId '{conversation.ConversationId}'.");
                }
            }
        }
    }

    private static async Task SetPreserveLastModifiedAtAsync(
        SqlConnection conn,
        SqlTransaction transaction,
        bool enabled,
        CancellationToken cancellationToken)
    {
        const string query = """
            EXEC sys.sp_set_session_context
                @key = N'ClaudeLogPreserveLastModifiedAt',
                @value = @Value;
            """;

        using var command = new SqlCommand(query, conn, transaction);
        command.Parameters.AddWithValue("@Value", enabled ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertSessionAsync(
        SqlConnection conn,
        SqlTransaction transaction,
        CoreDataSessionRecord session,
        CoreDataImportSummary summary,
        CancellationToken cancellationToken)
    {
        const string selectQuery = """
            SELECT LastModifiedAt
            FROM dbo.Sessions
            WHERE SessionId = @SessionId
            """;

        using var selectCommand = new SqlCommand(selectQuery, conn, transaction);
        selectCommand.Parameters.AddWithValue("@SessionId", session.SessionId);
        var existingLastModifiedAt = await selectCommand.ExecuteScalarAsync(cancellationToken);

        if (existingLastModifiedAt == null || existingLastModifiedAt == DBNull.Value)
        {
            const string insertQuery = """
                INSERT INTO dbo.Sessions (SessionId, Tool, IsDeleted, CreatedAt, LastModifiedAt)
                VALUES (@SessionId, @Tool, @IsDeleted, @CreatedAt, @LastModifiedAt)
                """;

            using var insertCommand = new SqlCommand(insertQuery, conn, transaction);
            insertCommand.Parameters.AddWithValue("@SessionId", session.SessionId);
            insertCommand.Parameters.AddWithValue("@Tool", session.Tool);
            insertCommand.Parameters.AddWithValue("@IsDeleted", session.IsDeleted);
            insertCommand.Parameters.AddWithValue("@CreatedAt", session.CreatedAt);
            insertCommand.Parameters.AddWithValue("@LastModifiedAt", session.LastModifiedAt);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            summary.SessionsInserted++;
            return;
        }

        if (await IsSessionSoftDeletedAsync(conn, transaction, session.SessionId, cancellationToken))
        {
            summary.SessionsSkipped++;
            return;
        }

        if (session.LastModifiedAt <= (DateTime)existingLastModifiedAt)
        {
            summary.SessionsSkipped++;
            return;
        }

        const string updateQuery = """
            UPDATE dbo.Sessions
            SET Tool = @Tool,
                IsDeleted = @IsDeleted,
                LastModifiedAt = @LastModifiedAt
            WHERE SessionId = @SessionId
            """;

        using var updateCommand = new SqlCommand(updateQuery, conn, transaction);
        updateCommand.Parameters.AddWithValue("@SessionId", session.SessionId);
        updateCommand.Parameters.AddWithValue("@Tool", session.Tool);
        updateCommand.Parameters.AddWithValue("@IsDeleted", session.IsDeleted);
        updateCommand.Parameters.AddWithValue("@LastModifiedAt", session.LastModifiedAt);
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        summary.SessionsUpdated++;
    }

    private static async Task UpsertConversationAsync(
        SqlConnection conn,
        SqlTransaction transaction,
        string sessionId,
        CoreDataConversationRecord conversation,
        CoreDataImportSummary summary,
        CancellationToken cancellationToken)
    {
        const string selectQuery = """
            SELECT LastModifiedAt
            FROM dbo.Conversations
            WHERE ConversationId = @ConversationId
            """;

        using var selectCommand = new SqlCommand(selectQuery, conn, transaction);
        selectCommand.Parameters.AddWithValue("@ConversationId", conversation.ConversationId);
        var existingLastModifiedAt = await selectCommand.ExecuteScalarAsync(cancellationToken);

        if (existingLastModifiedAt == null || existingLastModifiedAt == DBNull.Value)
        {
            const string insertQuery = """
                INSERT INTO dbo.Conversations (
                    ConversationId,
                    SessionId,
                    Title,
                    Question,
                    Response,
                    IsFavorite,
                    IsDeleted,
                    CreatedAt,
                    LastModifiedAt)
                VALUES (
                    @ConversationId,
                    @SessionId,
                    @Title,
                    @Question,
                    @Response,
                    @IsFavorite,
                    @IsDeleted,
                    @CreatedAt,
                    @LastModifiedAt)
                """;

            using var insertCommand = new SqlCommand(insertQuery, conn, transaction);
            insertCommand.Parameters.AddWithValue("@ConversationId", conversation.ConversationId);
            insertCommand.Parameters.AddWithValue("@SessionId", sessionId);
            insertCommand.Parameters.AddWithValue("@Title", conversation.Title);
            insertCommand.Parameters.AddWithValue("@Question", conversation.Question);
            insertCommand.Parameters.AddWithValue("@Response", conversation.Response);
            insertCommand.Parameters.AddWithValue("@IsFavorite", conversation.IsFavorite);
            insertCommand.Parameters.AddWithValue("@IsDeleted", conversation.IsDeleted);
            insertCommand.Parameters.AddWithValue("@CreatedAt", conversation.CreatedAt);
            insertCommand.Parameters.AddWithValue("@LastModifiedAt", conversation.LastModifiedAt);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            summary.ConversationsInserted++;
            return;
        }

        if (await IsConversationSoftDeletedAsync(conn, transaction, conversation.ConversationId, cancellationToken))
        {
            summary.ConversationsSkipped++;
            return;
        }

        if (conversation.LastModifiedAt <= (DateTime)existingLastModifiedAt)
        {
            summary.ConversationsSkipped++;
            return;
        }

        const string updateQuery = """
            UPDATE dbo.Conversations
            SET SessionId = @SessionId,
                Title = @Title,
                Question = @Question,
                Response = @Response,
                IsFavorite = @IsFavorite,
                IsDeleted = @IsDeleted,
                LastModifiedAt = @LastModifiedAt
            WHERE ConversationId = @ConversationId
            """;

        using var updateCommand = new SqlCommand(updateQuery, conn, transaction);
        updateCommand.Parameters.AddWithValue("@ConversationId", conversation.ConversationId);
        updateCommand.Parameters.AddWithValue("@SessionId", sessionId);
        updateCommand.Parameters.AddWithValue("@Title", conversation.Title);
        updateCommand.Parameters.AddWithValue("@Question", conversation.Question);
        updateCommand.Parameters.AddWithValue("@Response", conversation.Response);
        updateCommand.Parameters.AddWithValue("@IsFavorite", conversation.IsFavorite);
        updateCommand.Parameters.AddWithValue("@IsDeleted", conversation.IsDeleted);
        updateCommand.Parameters.AddWithValue("@LastModifiedAt", conversation.LastModifiedAt);
        await updateCommand.ExecuteNonQueryAsync(cancellationToken);
        summary.ConversationsUpdated++;
    }

    private static async Task<bool> IsSessionSoftDeletedAsync(
        SqlConnection conn,
        SqlTransaction transaction,
        string sessionId,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT IsDeleted
            FROM dbo.Sessions
            WHERE SessionId = @SessionId
            """;

        using var command = new SqlCommand(query, conn, transaction);
        command.Parameters.AddWithValue("@SessionId", sessionId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && result != DBNull.Value && (bool)result;
    }

    private static async Task<bool> IsConversationSoftDeletedAsync(
        SqlConnection conn,
        SqlTransaction transaction,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT IsDeleted
            FROM dbo.Conversations
            WHERE ConversationId = @ConversationId
            """;

        using var command = new SqlCommand(query, conn, transaction);
        command.Parameters.AddWithValue("@ConversationId", conversationId);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null && result != DBNull.Value && (bool)result;
    }
}
