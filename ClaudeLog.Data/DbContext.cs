using System;
using Microsoft.Data.SqlClient;

namespace ClaudeLog.Data;

public class DbContext
{
    private readonly string _connectionString;

    public DbContext()
    {
        _connectionString = Environment.GetEnvironmentVariable("CLAUDELOG_CONNECTION_STRING")
            ?? throw new InvalidOperationException("CLAUDELOG_CONNECTION_STRING environment variable is not set. Please configure it before running ClaudeLog.");
    }

    public SqlConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
