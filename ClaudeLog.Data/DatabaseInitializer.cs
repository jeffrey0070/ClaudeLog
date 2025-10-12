using Microsoft.Data.SqlClient;
using System.Reflection;

namespace ClaudeLog.Data;

/// <summary>
/// Handles automatic database initialization and schema migrations.
/// </summary>
public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Checks the database state and performs initialization or upgrade as needed.
    /// </summary>
    /// <returns>True if initialization succeeded, false if failed.</returns>
    public async Task<bool> InitializeAsync()
    {
        try
        {
            SqlConnectionStringBuilder builder;
            try
            {
                builder = new SqlConnectionStringBuilder(_connectionString);
            }
            catch (ArgumentException)
            {
                ShowConnectionError(_connectionString);
                return false;
            }

            var databaseName = builder.InitialCatalog;

            // Validate database name in connection string
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                Console.WriteLine("========================================");
                Console.WriteLine("ERROR: Database name not specified in connection string");
                Console.WriteLine("========================================");
                Console.WriteLine();
                Console.WriteLine("The connection string must include 'Database=YourDatabaseName'");
                Console.WriteLine($"Current connection string: {_connectionString}");
                Console.WriteLine("========================================");
                return false;
            }

            // Test connection to SQL Server (not the database)
            if (!await CanConnectToServerAsync())
            {
                ShowConnectionError(_connectionString);
                return false;
            }

            // Check if database exists
            if (!await DatabaseExistsAsync(databaseName))
            {
                Console.WriteLine($"Database '{databaseName}' does not exist. Creating...");
                await CreateDatabaseAsync(databaseName);
                await RunAllMigrationScriptsAsync();
                Console.WriteLine($"Database '{databaseName}' created successfully.");
            }
            else
            {
                // Database exists - check version and run pending migrations
                var currentVersion = await GetDatabaseVersionAsync();
                await RunPendingMigrationsAsync(currentVersion);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: Database initialization failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CanConnectToServerAsync()
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(_connectionString)
            {
                InitialCatalog = "master"
            };

            using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ShowConnectionError(string connectionString)
    {
        Console.WriteLine("========================================");
        Console.WriteLine($"ERROR: Cannot connect to SQL Server {connectionString}");
        Console.WriteLine("========================================");
        Console.WriteLine();
        Console.WriteLine("Please check your connection string configuration:");
        Console.WriteLine();
        Console.WriteLine("1. Update appsettings.json:");
        Console.WriteLine("   \"ConnectionStrings\": {");
        Console.WriteLine("     \"ClaudeLog\": \"Server=YOUR_SERVER;Database=ClaudeLog;Integrated Security=true;TrustServerCertificate=true;\"");
        Console.WriteLine("   }");
        Console.WriteLine();
        Console.WriteLine("2. Or set environment variable:");
        Console.WriteLine("   CLAUDELOG_CONNECTION_STRING=\"Server=YOUR_SERVER;Database=ClaudeLog;...\"");
        Console.WriteLine();
        Console.WriteLine("Common SQL Server names:");
        Console.WriteLine("  - localhost");
        Console.WriteLine("  - (localdb)\\MSSQLLocalDB");
        Console.WriteLine("  - .\\SQLEXPRESS");
        Console.WriteLine("========================================");
    }

    private async Task<bool> DatabaseExistsAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(
            "SELECT COUNT(*) FROM sys.databases WHERE name = @DatabaseName",
            connection);
        command.Parameters.AddWithValue("@DatabaseName", databaseName);

        var count = (int)await command.ExecuteScalarAsync();
        return count > 0;
    }

    private async Task CreateDatabaseAsync(string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand($"CREATE DATABASE [{databaseName}]", connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task RunAllMigrationScriptsAsync()
    {
        var scripts = GetMigrationScripts();

        if (scripts.Count == 0)
        {
            Console.WriteLine("WARNING: No migration scripts found in embedded resources.");
            return;
        }

        Console.WriteLine($"Running {scripts.Count} migration script(s)...");

        foreach (var (version, scriptContent) in scripts)
        {
            Console.WriteLine($"  Applying migration {version}...");
            await ExecuteSqlScriptAsync(scriptContent);
            await SetDatabaseVersionAsync(version);
        }

        var latestVersion = scripts[^1].Version;
        Console.WriteLine($"Database initialized to version {latestVersion}");
    }

    private async Task RunPendingMigrationsAsync(string? currentVersion)
    {
        var scripts = GetMigrationScripts();

        if (scripts.Count == 0)
        {
            Console.WriteLine("WARNING: No migration scripts found in embedded resources.");
            return;
        }

        var latestVersion = scripts[^1].Version;

        if (string.IsNullOrEmpty(currentVersion))
        {
            Console.WriteLine("Database has no version information. Running all migrations...");
            await RunAllMigrationScriptsAsync();
            return;
        }

        var currentVersionObj = Version.Parse(currentVersion);
        var pendingScripts = scripts.Where(s => Version.Parse(s.Version) > currentVersionObj).ToList();

        if (pendingScripts.Count == 0)
        {
            Console.WriteLine($"Database is up to date (version {currentVersion})");
            return;
        }

        Console.WriteLine($"Database version {currentVersion} found. Latest version is {latestVersion}.");
        Console.WriteLine($"Running {pendingScripts.Count} pending migration(s)...");

        foreach (var (version, scriptContent) in pendingScripts)
        {
            Console.WriteLine($"  Applying migration {version}...");
            await ExecuteSqlScriptAsync(scriptContent);
            await SetDatabaseVersionAsync(version);
        }

        Console.WriteLine($"Database upgraded to version {latestVersion}");
    }

    private List<(string Version, string ScriptContent)> GetMigrationScripts()
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("ClaudeLog.Data.Scripts.") && name.EndsWith(".sql"))
            .ToList();

        var scripts = new List<(string Version, string ScriptContent)>();

        foreach (var resourceName in resourceNames)
        {
            // Extract version from resource name: "ClaudeLog.Data.Scripts.1.0.0.sql" -> "1.0.0"
            var fileName = resourceName.Replace("ClaudeLog.Data.Scripts.", "").Replace(".sql", "");

            if (!IsVersionedScript(fileName))
                continue;

            var scriptContent = GetEmbeddedResource(resourceName);
            scripts.Add((fileName, scriptContent));
        }

        return scripts.OrderBy(s => Version.Parse(s.Version)).ToList();
    }

    private bool IsVersionedScript(string filename)
    {
        return Version.TryParse(filename, out _);
    }

    private string GetEmbeddedResource(string resourceName)
    {
        var assembly = typeof(DatabaseInitializer).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream == null)
        {
            throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task ExecuteSqlScriptAsync(string script)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        // Use transaction to ensure all-or-nothing execution
        using var transaction = connection.BeginTransaction();

        try
        {
            // Split by GO statements
            var batches = script.Split(new[] { "\nGO", "\r\nGO" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var batch in batches)
            {
                var trimmedBatch = batch.Trim();
                if (string.IsNullOrWhiteSpace(trimmedBatch))
                    continue;

                using var command = new SqlCommand(trimmedBatch, connection, transaction);
                command.CommandTimeout = 120; // 2 minutes timeout for large migrations
                await command.ExecuteNonQueryAsync();
            }

            // Commit if all batches succeed
            await transaction.CommitAsync();
        }
        catch
        {
            // Rollback on any failure
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string?> GetDatabaseVersionAsync()
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Check if DatabaseVersion table exists first
            using var checkCommand = new SqlCommand(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DatabaseVersion'",
                connection);

            var tableExists = (int)await checkCommand.ExecuteScalarAsync() > 0;
            if (!tableExists)
                return null;

            // Get the latest version
            using var command = new SqlCommand(
                "SELECT TOP 1 Version FROM dbo.DatabaseVersion ORDER BY AppliedAt DESC",
                connection);

            var result = await command.ExecuteScalarAsync();
            return result?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private async Task SetDatabaseVersionAsync(string version)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqlCommand(
            "INSERT INTO dbo.DatabaseVersion (Version, AppliedAt) VALUES (@Version, SYSDATETIME())",
            connection);
        command.Parameters.AddWithValue("@Version", version);

        await command.ExecuteNonQueryAsync();
    }
}
