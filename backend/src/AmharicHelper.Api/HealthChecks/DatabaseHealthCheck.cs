using System.Data.Common;
using AmharicHelper.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AmharicHelper.Api.HealthChecks;

/// <summary>
/// Verifies Postgres actually accepts a connection. Replaces the old static /health endpoint
/// (`Results.Ok(new { status = "ok" })`), which could never fail — Railway would report the app
/// healthy while the database was down and every real request 500'd.
/// </summary>
public class DatabaseHealthCheck(ISqlConnectionFactory factory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var conn = (DbConnection)factory.Create();
            await conn.OpenAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }
}
