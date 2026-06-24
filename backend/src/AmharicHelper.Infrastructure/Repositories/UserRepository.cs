using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class UserRepository(ISqlConnectionFactory factory) : IUserRepository
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM dbo.Users WHERE Id = @id", new { id });
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            "SELECT * FROM dbo.Users WHERE Email = @email", new { email });
    }

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.Users (Id, Email, PasswordHash, DisplayName, PreferredLanguage, CreatedAt)
            VALUES (@Id, @Email, @PasswordHash, @DisplayName, @PreferredLanguage, @CreatedAt)
            """, user);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            UPDATE dbo.Users
            SET Email = @Email, PasswordHash = @PasswordHash, DisplayName = @DisplayName,
                PreferredLanguage = @PreferredLanguage
            WHERE Id = @Id
            """, user);
    }
}
