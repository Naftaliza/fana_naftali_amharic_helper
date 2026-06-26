using System.Data;
using Npgsql;

namespace AmharicHelper.Infrastructure.Persistence;

public interface ISqlConnectionFactory
{
    IDbConnection Create();
    string ConnectionString { get; }
}

/// <summary>
/// Creates PostgreSQL connections. Accepts either a native Npgsql key/value
/// connection string or a URL form (postgres://user:pass@host:port/db) — Railway's
/// Postgres plugin exposes the latter as DATABASE_URL, so it can be referenced directly.
/// </summary>
public class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public string ConnectionString { get; } = Normalize(connectionString);

    public IDbConnection Create() => new NpgsqlConnection(ConnectionString);

    /// <summary>Converts a postgres:// URL into an Npgsql key/value string; passes through key/value strings unchanged.</summary>
    public static string Normalize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;
        if (!raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return raw;

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.TrimStart('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
            Username = user,
            Password = password,
            Database = database,
            // Railway's internal hostname doesn't use TLS; the public proxy does. Prefer
            // negotiates TLS when offered and falls back when it isn't, so both work.
            SslMode = SslMode.Prefer,
            TrustServerCertificate = true,
        };
        return builder.ConnectionString;
    }
}
