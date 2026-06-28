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

    /// <summary>Raw row matching the SQL columns; the JSON Blurb is deserialized in <see cref="ToEntity"/>.</summary>
    private class ProviderRow
    {
        public Guid Id { get; set; }
        public int Category { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? WhatsApp { get; set; }
        public string? City { get; set; }
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
            Blurb = JsonSerializer.Deserialize<LocalizedText>(Blurb) ?? new(),
            IsActive = IsActive,
            Priority = Priority,
            CreatedAt = CreatedAt
        };
    }
}
