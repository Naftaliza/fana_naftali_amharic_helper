using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class LeadRepository(ISqlConnectionFactory factory) : ILeadRepository
{
    public async Task<IReadOnlyList<LeadSummaryDto>> GetSummaryAsync(CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<LeadSummaryDto>(
            """
            SELECT p.Id AS ProviderId, p.DisplayName,
                   COUNT(*) FILTER (WHERE l.CreatedAt >= date_trunc('month', now()))::int AS MonthCount,
                   COUNT(*)::int AS TotalCount
            FROM Leads l JOIN Providers p ON p.Id = l.ProviderId
            GROUP BY p.Id, p.DisplayName
            ORDER BY MonthCount DESC, TotalCount DESC
            """);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<RecentLeadDto>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<RecentLeadDto>(
            """
            SELECT l.ProviderId, p.DisplayName, l.Category, l.Urgency, l.CreatedAt
            FROM Leads l JOIN Providers p ON p.Id = l.ProviderId
            ORDER BY l.CreatedAt DESC
            LIMIT @limit
            """, new { limit });
        return rows.ToList();
    }

    public async Task AddAsync(Lead lead, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO Leads (Id, ProviderId, Category, Urgency, DocumentId, CreatedAt)
            VALUES (@Id, @ProviderId, @Category, @Urgency, @DocumentId, @CreatedAt)
            """,
            new
            {
                lead.Id,
                lead.ProviderId,
                Category = (int)lead.Category,
                Urgency = (int)lead.Urgency,
                lead.DocumentId,
                lead.CreatedAt
            });
    }
}
