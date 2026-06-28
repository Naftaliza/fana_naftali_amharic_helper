using System.Text.Json;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class ProviderRepository(ISqlConnectionFactory factory) : IProviderRepository
{
    public async Task<IReadOnlyList<Provider>> GetActiveByCategoryAsync(
        DocumentCategory category, int limit, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<ProviderRow>(
            """
            SELECT * FROM Providers
            WHERE IsActive = TRUE AND Category = @category
            ORDER BY Priority DESC, DisplayName ASC
            LIMIT @limit
            """,
            new { category = (int)category, limit });
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<Provider?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<ProviderRow>(
            "SELECT * FROM Providers WHERE Id = @id", new { id });
        return row?.ToEntity();
    }

    public async Task AddAsync(Provider provider, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO Providers
                (Id, Category, DisplayName, Phone, WhatsApp, City, ContactEmail, Blurb, IsActive, Priority, CreatedAt)
            VALUES
                (@Id, @Category, @DisplayName, @Phone, @WhatsApp, @City, @ContactEmail, @Blurb, @IsActive, @Priority, @CreatedAt)
            """,
            new
            {
                provider.Id,
                Category = (int)provider.Category,
                provider.DisplayName,
                provider.Phone,
                provider.WhatsApp,
                provider.City,
                provider.ContactEmail,
                Blurb = JsonSerializer.Serialize(provider.Blurb),
                provider.IsActive,
                provider.Priority,
                provider.CreatedAt
            });
    }

    public async Task<IReadOnlyList<Provider>> GetPendingAsync(CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // Only never-reviewed applications — deactivated providers stay out of this queue.
        var rows = await conn.QueryAsync<ProviderRow>(
            "SELECT * FROM Providers WHERE IsActive = FALSE AND ReviewedAt IS NULL ORDER BY CreatedAt DESC");
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task<IReadOnlyList<Provider>> GetManagedAsync(CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // Everything an admin has acted on (live + deactivated), live first.
        var rows = await conn.QueryAsync<ProviderRow>(
            "SELECT * FROM Providers WHERE ReviewedAt IS NOT NULL ORDER BY IsActive DESC, Category, Priority DESC, DisplayName");
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        // Stamp ReviewedAt the first time (approve or deactivate) so it's never seen as "pending" again.
        await conn.ExecuteAsync(
            "UPDATE Providers SET IsActive = @active, ReviewedAt = COALESCE(ReviewedAt, now()) WHERE Id = @id",
            new { id, active });
    }

    public async Task UpdateAsync(Provider provider, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            UPDATE Providers SET
                Category = @Category, DisplayName = @DisplayName, Phone = @Phone, WhatsApp = @WhatsApp,
                City = @City, ContactEmail = @ContactEmail, Blurb = @Blurb, Priority = @Priority
            WHERE Id = @Id
            """,
            new
            {
                provider.Id,
                Category = (int)provider.Category,
                provider.DisplayName,
                provider.Phone,
                provider.WhatsApp,
                provider.City,
                provider.ContactEmail,
                Blurb = JsonSerializer.Serialize(provider.Blurb),
                provider.Priority
            });
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("DELETE FROM Providers WHERE Id = @id", new { id });
    }

    /// <summary>Raw row matching the SQL columns; the JSON Blurb is deserialized in <see cref="ToEntity"/>.</summary>
    private class ProviderRow
    {
        public Guid Id { get; set; }
        public int Category { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? WhatsApp { get; set; }
        public string? City { get; set; }
        public string? ContactEmail { get; set; }
        public string Blurb { get; set; } = "{}";
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public DateTime CreatedAt { get; set; }

        public Provider ToEntity() => new()
        {
            Id = Id,
            Category = (DocumentCategory)Category,
            DisplayName = DisplayName,
            Phone = Phone,
            WhatsApp = WhatsApp,
            City = City,
            ContactEmail = ContactEmail,
            Blurb = JsonSerializer.Deserialize<LocalizedText>(Blurb) ?? new(),
            IsActive = IsActive,
            Priority = Priority,
            CreatedAt = CreatedAt
        };
    }
}
