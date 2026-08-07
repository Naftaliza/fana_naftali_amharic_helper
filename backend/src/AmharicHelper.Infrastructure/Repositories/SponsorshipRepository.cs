using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class SponsorshipRepository(ISqlConnectionFactory factory) : ISponsorshipRepository
{
    public async Task AddAsync(Sponsorship sponsorship, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO Sponsorships
                (Id, SponsorUserId, BeneficiaryContactHash, Credits, RedeemTokenHash,
                 RedeemedByUserId, RedeemedAt, ExpiresAt, CreatedAt)
            VALUES
                (@Id, @SponsorUserId, @BeneficiaryContactHash, @Credits, @RedeemTokenHash,
                 @RedeemedByUserId, @RedeemedAt, @ExpiresAt, @CreatedAt)
            """,
            sponsorship);
    }

    public async Task<Sponsorship?> GetByRedeemTokenHashAsync(string redeemTokenHash, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Sponsorship>(
            "SELECT * FROM Sponsorships WHERE RedeemTokenHash = @redeemTokenHash", new { redeemTokenHash });
    }

    public async Task<bool> TryMarkRedeemedAsync(Guid id, Guid redeemedByUserId, DateTime redeemedAt, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // WHERE RedeemedByUserId IS NULL is the concurrency guard: only the first of two
        // simultaneous attempts affects a row, so only it gets rowsAffected == 1.
        var rowsAffected = await conn.ExecuteAsync(
            """
            UPDATE Sponsorships SET RedeemedByUserId = @redeemedByUserId, RedeemedAt = @redeemedAt
            WHERE Id = @id AND RedeemedByUserId IS NULL
            """,
            new { id, redeemedByUserId, redeemedAt });
        return rowsAffected == 1;
    }

    public async Task<IReadOnlyList<Sponsorship>> ListBySponsorAsync(Guid sponsorUserId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<Sponsorship>(
            "SELECT * FROM Sponsorships WHERE SponsorUserId = @sponsorUserId ORDER BY CreatedAt DESC",
            new { sponsorUserId });
        return rows.ToList();
    }
}
