using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class LeadRepository(ISqlConnectionFactory factory) : ILeadRepository
{
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
