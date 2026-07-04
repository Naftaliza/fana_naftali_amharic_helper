using AmharicHelper.Domain.Entities;

namespace AmharicHelper.Application.DTOs;

/// <summary>Public branding info a white-labeled front end fetches by slug to theme itself.</summary>
public record OrganizationBrandingDto(
    string Slug,
    string Name,
    string? LogoUrl,
    string PrimaryColorHex,
    string? AccentColorHex,
    LocalizedText WelcomeText);

/// <summary>Admin: create a new tenant. WelcomeText fills all three language slots (an admin
/// can refine the individual translations later, same pattern as a provider's Blurb).</summary>
public record CreateOrganizationRequest(
    string Name,
    string Slug,
    string? LogoUrl,
    string PrimaryColorHex,
    string? AccentColorHex,
    string WelcomeText);

/// <summary>Admin: edit an existing tenant's branding. Slug is intentionally excluded — it's
/// locked after creation because it drives already-shared ?org=slug links and tenant
/// resolution; WelcomeText here replaces all three language slots, same simplification as
/// CreateOrganizationRequest.</summary>
public record UpdateOrganizationRequest(
    string Name,
    string? LogoUrl,
    string PrimaryColorHex,
    string? AccentColorHex,
    string WelcomeText);

/// <summary>Admin: one row in the tenant list. Includes branding so the admin console (e.g.
/// the usage dashboard) can echo the tenant's own identity without a second round trip.</summary>
public record OrganizationSummaryDto(
    Guid Id, string Name, string Slug, bool IsActive, DateTime CreatedAt,
    string? LogoUrl, string PrimaryColorHex, string? AccentColorHex, string WelcomeText);

public record CategoryCountDto(int Category, int Count);
public record UrgencyCountDto(int Urgency, int Count);
public record WeeklyCountDto(DateTime WeekStart, int Count);

/// <summary>Admin: aggregate, anonymized usage for one tenant — the evidence a buyer's budget
/// owner needs to justify renewal. UrgentCount is documents analyzed at High or Critical
/// urgency; no personal document content is included.</summary>
public record OrganizationStatsDto(
    int DocumentsProcessed,
    int UniqueUsers,
    int UrgentCount,
    IReadOnlyList<CategoryCountDto> ByCategory,
    IReadOnlyList<UrgencyCountDto> ByUrgency,
    IReadOnlyList<WeeklyCountDto> WeeklyTrend);
