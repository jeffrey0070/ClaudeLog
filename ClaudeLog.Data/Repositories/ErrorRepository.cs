using ClaudeLog.Data.Models;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data.Repositories;

public class ErrorRepository
{
    private readonly DbContext _dbContext;

    private const string InsertErrorLogQuery = @"
        INSERT INTO dbo.ErrorLogs (Source, Message, Detail, Path, SectionId, EntryId, CreatedAt)
        OUTPUT INSERTED.Id
        VALUES (@Source, @Message, @Detail, @Path, @SectionId, @EntryId, @CreatedAt)";

    public ErrorRepository(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LogErrorResponse> LogErrorAsync(LogErrorRequest request)
    {
        var createdAt = request.CreatedAt ?? DateTime.Now;

        using var conn = _dbContext.CreateConnection();
        await conn.OpenAsync();

        using var cmd = new SqlCommand(InsertErrorLogQuery, conn);
        cmd.Parameters.AddWithValue("@Source", request.Source);
        cmd.Parameters.AddWithValue("@Message", request.Message);
        cmd.Parameters.AddWithValue("@Detail", (object?)request.Detail ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Path", (object?)request.Path ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SectionId", (object?)request.SectionId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@EntryId", (object?)request.EntryId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", createdAt);

        var id = (long)await cmd.ExecuteScalarAsync();

        return new LogErrorResponse(true, id);
    }
}
