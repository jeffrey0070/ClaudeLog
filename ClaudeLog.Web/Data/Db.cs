using Microsoft.Data.SqlClient;

namespace ClaudeLog.Web.Data;

public class Db
{
    private readonly string _connectionString;

    public Db(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ClaudeLog")
            ?? throw new InvalidOperationException("Connection string 'ClaudeLog' not found.");
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
