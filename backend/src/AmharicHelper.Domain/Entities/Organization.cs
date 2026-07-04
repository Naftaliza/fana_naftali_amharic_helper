namespace AmharicHelper.Domain.Entities;

/// <summary>
/// A B2B/B2G tenant (municipality, health fund, NGO, ...) licensing Fana for its own
/// residents/members. A branded front end resolves itself by <see cref="Slug"/>; a
/// <see cref="User"/> tagged with this org's Id is one of that tenant's members. The
/// underlying document pipeline is identical for every tenant — only branding and
/// aggregate usage reporting are tenant-specific.
/// </summary>
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe identifier used to resolve branding (e.g. "netanya"). Lowercase, unique.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? LogoUrl { get; set; }

    /// <summary>Hex color driving the tenant's primary accent in the white-labeled front end.</summary>
    public string PrimaryColorHex { get; set; } = "#2563EB";
    public string? AccentColorHex { get; set; }

    /// <summary>Short welcome message shown on the tenant's landing, localized to all three languages.</summary>
    public LocalizedText WelcomeText { get; set; } = new();

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
