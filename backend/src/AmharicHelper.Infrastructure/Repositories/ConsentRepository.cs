using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class ConsentRepository(ISqlConnectionFactory factory) : IConsentRepository
{
    public async Task AddAsync(ConsentRecord record, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO ConsentRecords (Id, UserId, ContactHash, Kind, Version, AcceptedAt, SourceIp)
            VALUES (@Id, @UserId, @ContactHash, @Kind, @Version, @AcceptedAt, @SourceIp)
            """,
            record);
    }
}
