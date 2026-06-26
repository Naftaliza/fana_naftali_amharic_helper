using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class ChatMessageRepository(ISqlConnectionFactory factory) : IChatMessageRepository
{
    public async Task<IReadOnlyList<ChatMessage>> ListByDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<ChatMessage>(
            "SELECT * FROM ChatMessages WHERE DocumentId = @documentId ORDER BY CreatedAt ASC",
            new { documentId });
        return rows.ToList();
    }

    public async Task AddAsync(ChatMessage message, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO ChatMessages (Id, DocumentId, Role, Content, CreatedAt)
            VALUES (@Id, @DocumentId, @Role, @Content, @CreatedAt)
            """,
            new { message.Id, message.DocumentId, Role = (int)message.Role, message.Content, message.CreatedAt });
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM ChatMessages WHERE DocumentId = @documentId", new { documentId });
    }
}
