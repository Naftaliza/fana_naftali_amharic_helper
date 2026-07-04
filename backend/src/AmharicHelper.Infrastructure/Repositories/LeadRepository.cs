using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
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
                   COUNT(*)::int AS TotalCount,
                   p.PricePerLead AS PricePerLead,
                   (COUNT(*) FILTER (WHERE l.CreatedAt >= date_trunc('month', now())) * p.PricePerLead)::numeric AS MonthAmount,
                   (COUNT(*) * p.PricePerLead)::numeric AS TotalAmount,
                   COUNT(*) FILTER (WHERE l.CreatedAt >= date_trunc('month', now()) AND l.Status = 3)::int AS ConvertedMonthCount,
                   COUNT(*) FILTER (WHERE l.Status = 3)::int AS ConvertedTotalCount,
                   (COUNT(*) FILTER (WHERE l.CreatedAt >= date_trunc('month', now()) AND l.Status = 3) * p.PricePerLead)::numeric AS BillableMonthAmount,
                   (COUNT(*) FILTER (WHERE l.Status = 3) * p.PricePerLead)::numeric AS BillableTotalAmount,
                   (COUNT(*) FILTER (WHERE l.Helpful = true))::float8 / NULLIF(COUNT(*) FILTER (WHERE l.Helpful IS NOT NULL), 0) AS HelpfulRate,
                   p.ContactEmail AS ContactEmail
            FROM Leads l JOIN Providers p ON p.Id = l.ProviderId
            GROUP BY p.Id, p.DisplayName, p.PricePerLead, p.ContactEmail
            ORDER BY MonthCount DESC, TotalCount DESC
            """);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<RecentLeadDto>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<RecentLeadDto>(
            """
            SELECT l.Id, l.ProviderId, p.DisplayName, l.Category, l.Urgency, l.Ref, l.Status, l.Helpful, l.CreatedAt
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
            INSERT INTO Leads (Id, ProviderId, Category, Urgency, DocumentId, Ref, Status, CreatedAt)
            VALUES (@Id, @ProviderId, @Category, @Urgency, @DocumentId, @Ref, @Status, @CreatedAt)
            """,
            new
            {
                lead.Id,
                lead.ProviderId,
                Category = (int)lead.Category,
                Urgency = (int)lead.Urgency,
                lead.DocumentId,
                lead.Ref,
                Status = (int)lead.Status,
                lead.CreatedAt
            });
    }

    public async Task UpdateStatusAsync(Guid leadId, LeadStatus status, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            "UPDATE Leads SET Status = @Status WHERE Id = @Id",
            new { Id = leadId, Status = (int)status });
    }

    public async Task<bool> SetFeedbackAsync(string refCode, bool helpful, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE Leads SET Helpful = @Helpful WHERE Ref = @Ref",
            new { Ref = refCode, Helpful = helpful });
        return rows > 0;
    }

    public async Task<IReadOnlyList<Lead>> GetConvertedForPeriodAsync(Guid providerId, int year, int month, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<LeadRow>(
            """
            SELECT Id, ProviderId, Category, Urgency, DocumentId, Ref, Status, Helpful, CreatedAt
            FROM Leads
            WHERE ProviderId = @providerId
              AND Status = 3
              AND CreatedAt >= make_date(@year, @month, 1)
              AND CreatedAt < make_date(@year, @month, 1) + INTERVAL '1 month'
            ORDER BY CreatedAt
            """,
            new { providerId, year, month });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    /// <summary>Raw row matching the SQL columns; int enum columns are cast in <see cref="ToEntity"/>.</summary>
    private class LeadRow
    {
        public Guid Id { get; set; }
        public Guid ProviderId { get; set; }
        public int Category { get; set; }
        public int Urgency { get; set; }
        public Guid? DocumentId { get; set; }
        public string? Ref { get; set; }
        public int Status { get; set; }
        public bool? Helpful { get; set; }
        public DateTime CreatedAt { get; set; }

        public Lead ToEntity() => new()
        {
            Id = Id,
            ProviderId = ProviderId,
            Category = (DocumentCategory)Category,
            Urgency = (UrgencyLevel)Urgency,
            DocumentId = DocumentId,
            Ref = Ref,
            Status = (LeadStatus)Status,
            Helpful = Helpful,
            CreatedAt = CreatedAt
        };
    }
}
