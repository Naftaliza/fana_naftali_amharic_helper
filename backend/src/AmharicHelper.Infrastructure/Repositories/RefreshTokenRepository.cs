using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class RefreshTokenRepository(ISqlConnectionFactory factory) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        return await conn.QuerySingleOrDefaultAsync<RefreshToken>(
            "SELECT * FROM RefreshTokens WHERE Token = @token", new { token });
    }

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO RefreshTokens (Id, UserId, Token, ExpiresAt, CreatedAt, RevokedAt)
            VALUES (@Id, @UserId, @Token, @ExpiresAt, @CreatedAt, @RevokedAt)
            """, token);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            "UPDATE RefreshTokens SET RevokedAt = now() WHERE Id = @id", new { id });
    }

    public async Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM RefreshTokens WHERE UserId = @userId", new { userId });
    }
}
