using System.Data;
using Microsoft.Data.SqlClient;

namespace AmharicHelper.Infrastructure.Persistence;

public interface ISqlConnectionFactory
{
    IDbConnection Create();
    string ConnectionString { get; }
}

public class SqlConnectionFactory(string connectionString) : ISqlConnectionFactory
{
    public string ConnectionString { get; } = connectionString;
    public IDbConnection Create() => new SqlConnection(ConnectionString);
}
