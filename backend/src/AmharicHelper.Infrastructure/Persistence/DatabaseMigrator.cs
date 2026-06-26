using Dapper;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Infrastructure.Persistence;

/// <summary>
/// Runs the ordered .sql scripts in the Migrations folder on startup. Scripts are
/// written to be idempotent (CREATE TABLE IF NOT EXISTS / ON CONFLICT), so re-running
/// them is safe. The target database is provided by the host (Railway Postgres plugin,
/// or POSTGRES_DB locally), so this only waits for it to accept connections, then migrates.
/// </summary>
public class DatabaseMigrator(ISqlConnectionFactory factory, ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken ct = default)
    {
        await WaitForDatabaseAsync(ct);

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
            if (string.IsNullOrWhiteSpace(sql)) continue;
            logger.LogInformation("Applying migration {File}", Path.GetFileName(file));
            // Npgsql executes a whole file (multiple ;-separated statements, including
            // dollar-quoted blocks) as a single command — no GO/batch splitting needed.
            await conn.ExecuteAsync(sql);
        }
    }

    /// <summary>
    /// Waits for the database to accept connections. The database can lag behind the API
    /// on a fresh deploy; retrying here keeps the API from crash-looping on startup.
    /// </summary>
    private async Task WaitForDatabaseAsync(CancellationToken ct)
    {
        const int maxAttempts = 30;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var conn = factory.Create();
                conn.Open();
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(
                    "Database not ready (attempt {Attempt}/{Max}): {Message}. Retrying in 2s...",
                    attempt, maxAttempts, ex.Message);
                await Task.Delay(2000, ct);
            }
        }
    }
}
