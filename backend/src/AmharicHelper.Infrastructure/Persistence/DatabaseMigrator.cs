using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Infrastructure.Persistence;

/// <summary>
/// Runs the ordered .sql scripts in the Migrations folder on startup. Scripts are written
/// to be idempotent, so re-running them is safe. Also creates the database if it is missing.
/// </summary>
public class DatabaseMigrator(ISqlConnectionFactory factory, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await EnsureDatabaseExistsAsync(ct);

        var dir = Path.Combine(AppContext.BaseDirectory, "Migrations");
        if (!Directory.Exists(dir))
        {
            logger.LogWarning("Migrations directory not found at {Dir}", dir);
            return;
        }

        using var conn = factory.Create();
        foreach (var file in Directory.GetFiles(dir, "*.sql").OrderBy(f => f))
        {
            var sql = await File.ReadAllTextAsync(file, ct);
            logger.LogInformation("Applying migration {File}", Path.GetFileName(file));
            // Split on GO batch separators if present.
            foreach (var batch in SplitBatches(sql))
            {
                if (!string.IsNullOrWhiteSpace(batch))
                    await conn.ExecuteAsync(batch);
            }
        }
    }

    private async Task EnsureDatabaseExistsAsync(CancellationToken ct)
    {
        var builder = new SqlConnectionStringBuilder(factory.ConnectionString);
        var dbName = builder.InitialCatalog;
        builder.InitialCatalog = "master";

        await using var conn = new SqlConnection(builder.ConnectionString);
        await conn.OpenAsync(ct);
        await conn.ExecuteAsync(
            $"IF DB_ID(@db) IS NULL EXEC('CREATE DATABASE [' + @db + ']');",
            new { db = dbName });
    }

    private static IEnumerable<string> SplitBatches(string sql) =>
        sql.Split(["\nGO\n", "\nGO\r\n", "\r\nGO\r\n"], StringSplitOptions.None);
}
