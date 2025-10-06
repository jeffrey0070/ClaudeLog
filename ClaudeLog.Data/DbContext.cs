using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data;

public class DbContext
{
    private readonly string _connectionString;

    public DbContext(string? connectionString = null)
    {
        _connectionString = connectionString
            ?? Environment.GetEnvironmentVariable("CLAUDELOG_CONNECTION_STRING")
            ?? "Server=localhost;Database=ClaudeLog;Integrated Security=true;TrustServerCertificate=true;";
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
