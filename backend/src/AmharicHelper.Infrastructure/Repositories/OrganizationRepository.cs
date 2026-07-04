using System.Text.Json;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Persistence;
using Dapper;

namespace AmharicHelper.Infrastructure.Repositories;

public class OrganizationRepository(ISqlConnectionFactory factory) : IOrganizationRepository
{
    public async Task<Organization?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<OrganizationRow>(
            "SELECT * FROM Organizations WHERE LOWER(Slug) = LOWER(@slug)", new { slug });
        return row?.ToEntity();
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var row = await conn.QuerySingleOrDefaultAsync<OrganizationRow>(
            "SELECT * FROM Organizations WHERE Id = @id", new { id });
        return row?.ToEntity();
    }

    public async Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default)
    {
        using var conn = factory.Create();
        var rows = await conn.QueryAsync<OrganizationRow>(
            "SELECT * FROM Organizations ORDER BY CreatedAt DESC");
        return rows.Select(r => r.ToEntity()).ToList();
    }

    public async Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            INSERT INTO Organizations
                (Id, Name, Slug, LogoUrl, PrimaryColorHex, AccentColorHex, WelcomeText, IsActive, CreatedAt)
            VALUES
                (@Id, @Name, @Slug, @LogoUrl, @PrimaryColorHex, @AccentColorHex, @WelcomeText, @IsActive, @CreatedAt)
            """,
            new
            {
                organization.Id,
                organization.Name,
                organization.Slug,
                organization.LogoUrl,
                organization.PrimaryColorHex,
                organization.AccentColorHex,
                WelcomeText = JsonSerializer.Serialize(organization.WelcomeText),
                organization.IsActive,
                organization.CreatedAt
            });
    }

    public async Task UpdateAsync(Organization organization, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync(
            """
            UPDATE Organizations SET
                Name = @Name, LogoUrl = @LogoUrl, PrimaryColorHex = @PrimaryColorHex,
                AccentColorHex = @AccentColorHex, WelcomeText = @WelcomeText
            WHERE Id = @Id
            """,
            new
            {
                organization.Id,
                organization.Name,
                organization.LogoUrl,
                organization.PrimaryColorHex,
                organization.AccentColorHex,
                WelcomeText = JsonSerializer.Serialize(organization.WelcomeText)
            });
    }

    public async Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default)
    {
        using var conn = factory.Create();
        await conn.ExecuteAsync("UPDATE Organizations SET IsActive = @active WHERE Id = @id", new { id, active });
    }

    public async Task<OrganizationStatsDto> GetStatsAsync(Guid organizationId, CancellationToken ct = default)
    {
        using var conn = factory.Create();

        // A document is "processed" once it has an analysis. Attribution flows
        // Organizations -> Users -> Documents -> DocumentAnalyses (no OrganizationId
        // denormalized onto Documents — the join is cheap at this scale).
        const string baseJoin = """
            FROM DocumentAnalyses da
            JOIN Documents d ON d.Id = da.DocumentId
            JOIN Users u ON u.Id = d.UserId
            WHERE u.OrganizationId = @organizationId
            """;

        var totals = await conn.QuerySingleAsync<TotalsRow>(
            $"""
            SELECT
                COUNT(*)::int AS Documents,
                COUNT(DISTINCT u.Id)::int AS Users,
                COUNT(*) FILTER (WHERE da.UrgencyLevel >= 2)::int AS Urgent
            {baseJoin}
            """,
            new { organizationId });

        var byCategory = await conn.QueryAsync<CategoryCountDto>(
            $"""
            SELECT da.Category AS Category, COUNT(*)::int AS Count
            {baseJoin}
            GROUP BY da.Category
            ORDER BY Count DESC
            """,
            new { organizationId });

        var byUrgency = await conn.QueryAsync<UrgencyCountDto>(
            $"""
            SELECT da.UrgencyLevel AS Urgency, COUNT(*)::int AS Count
            {baseJoin}
            GROUP BY da.UrgencyLevel
            ORDER BY da.UrgencyLevel
            """,
            new { organizationId });

        var weekly = await conn.QueryAsync<WeeklyCountDto>(
            $"""
            SELECT date_trunc('week', da.CreatedAt) AS WeekStart, COUNT(*)::int AS Count
            {baseJoin}
            GROUP BY 1
            ORDER BY 1
            """,
            new { organizationId });

        return new OrganizationStatsDto(
            totals.Documents, totals.Users, totals.Urgent,
            byCategory.ToList(), byUrgency.ToList(), weekly.ToList());
    }

    private class TotalsRow
    {
        public int Documents { get; set; }
        public int Users { get; set; }
        public int Urgent { get; set; }
    }

    /// <summary>Raw row matching the SQL columns; the JSON WelcomeText is deserialized in <see cref="ToEntity"/>.</summary>
    private class OrganizationRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string PrimaryColorHex { get; set; } = "#2563EB";
        public string? AccentColorHex { get; set; }
        public string WelcomeText { get; set; } = "{}";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public Organization ToEntity() => new()
        {
            Id = Id,
            Name = Name,
            Slug = Slug,
            LogoUrl = LogoUrl,
            PrimaryColorHex = PrimaryColorHex,
            AccentColorHex = AccentColorHex,
            WelcomeText = JsonSerializer.Deserialize<LocalizedText>(WelcomeText) ?? new(),
            IsActive = IsActive,
            CreatedAt = CreatedAt
        };
    }
}
