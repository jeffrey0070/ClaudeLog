using ClaudeLog.Web.Data;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Web.Services;

public class ErrorLogger
{
    private readonly Db _db;

    public ErrorLogger(Db db)
    {
        _db = db;
    }

    public async Task<long?> LogErrorAsync(
        string source,
        string message,
        string? detail = null,
        string? path = null,
        Guid? sectionId = null,
        long? entryId = null)
    {
        try
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new SqlCommand(ErrorLogQueries.InsertError, conn);
            cmd.Parameters.AddWithValue("@Source", source);
            cmd.Parameters.AddWithValue("@Message", message);
            cmd.Parameters.AddWithValue("@Detail", (object?)detail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Path", (object?)path ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@SectionId", (object?)sectionId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@EntryId", (object?)entryId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt64(result) : null;
        }
        catch
        {
            // Swallow errors in error logger to prevent infinite loops
            return null;
        }
    }

    public async Task LogExceptionAsync(
        string source,
        Exception ex,
        string? path = null,
        Guid? sectionId = null,
        long? entryId = null)
    {
        await LogErrorAsync(
            source,
            ex.Message,
            ex.StackTrace,
            path,
            sectionId,
            entryId);
    }
}
