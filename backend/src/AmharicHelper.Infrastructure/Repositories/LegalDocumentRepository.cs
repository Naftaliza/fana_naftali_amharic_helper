using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class LegalDocumentRepository(ISqlConnectionFactory factory) : ILegalDocumentRepository
{
    public async Task<LegalDocument?> GetLatestAsync(string kind, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        return await conn.QueryFirstOrDefaultAsync<LegalDocument>(
            "SELECT * FROM LegalDocuments WHERE Kind = @kind ORDER BY Version DESC LIMIT 1", new { kind });
    }
}
