using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class AnalyticsEventRepository(ISqlConnectionFactory factory) : IAnalyticsEventRepository
{
    public async Task AddAsync(AnalyticsEvent evt, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            "INSERT INTO AnalyticsEvents (Id, Name, UserId, CreatedAt) VALUES (@Id, @Name, @UserId, @CreatedAt)",
            evt);
    }

    public async Task<IReadOnlyList<EventCountDto>> GetFunnelCountsAsync(DateTime sinceUtc, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<EventCountDto>(
            """
            SELECT Name, COUNT(*)::int AS Count
            FROM AnalyticsEvents
            WHERE CreatedAt >= @sinceUtc
            GROUP BY Name
            ORDER BY Name
            """,
            new { sinceUtc });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<EventDetailDto>> GetEventDetailsAsync(string name, DateTime sinceUtc, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<EventDetailDto>(
            """
            SELECT u.Email, e.CreatedAt
            FROM AnalyticsEvents e
            LEFT JOIN Users u ON u.Id = e.UserId
            WHERE e.Name = @name AND e.CreatedAt >= @sinceUtc
            ORDER BY e.CreatedAt DESC
            """,
            new { name, sinceUtc });
        return rows.ToList();
    }
}
