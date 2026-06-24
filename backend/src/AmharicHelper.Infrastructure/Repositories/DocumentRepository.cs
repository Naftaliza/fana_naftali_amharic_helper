using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class DocumentRepository(ISqlConnectionFactory factory) : IDocumentRepository
{
    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Document>(
            "SELECT * FROM dbo.Documents WHERE Id = @id", new { id });
    }

    public async Task<IReadOnlyList<Document>> ListByUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<Document>(
            "SELECT * FROM dbo.Documents WHERE UserId = @userId ORDER BY UploadedAt DESC", new { userId });
        return rows.ToList();
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.Documents (Id, UserId, FileName, FilePath, ContentType, OcrText, UploadedAt)
            VALUES (@Id, @UserId, @FileName, @FilePath, @ContentType, @OcrText, @UploadedAt)
            """, document);
    }

    public async Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            "UPDATE dbo.Documents SET OcrText = @OcrText WHERE Id = @Id", document);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM dbo.Documents WHERE Id = @id", new { id });
    }
}
