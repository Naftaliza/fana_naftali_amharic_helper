using AmharicHelper.Domain.Entities;

namespace AmharicHelper.Application.DTOs;

/// <summary>A sponsored referral shown to the user. Category is the int DocumentCategory value.</summary>
public record ProviderDto(
    Guid Id,
    int Category,
    string DisplayName,
    string? Phone,
    string? WhatsApp,
    string? City,
    LocalizedText Blurb);

/// <summary>Body for logging a lead. All fields optional; category/urgency are int enum values.</summary>
public record LogLeadRequest(int? Category, int? Urgency, Guid? DocumentId);

/// <summary>Public business self-registration. Category is the int DocumentCategory value.</summary>
public record ProviderApplicationRequest(
    string DisplayName,
    int Category,
    string? City,
    string? Phone,
    string? WhatsApp,
    string ContactEmail,
    string Description);

/// <summary>A pending application shown to the admin (includes contact details).</summary>
public record PendingProviderDto(
    Guid Id,
    int Category,
    string DisplayName,
    string? City,
    string? Phone,
    string? WhatsApp,
    string? ContactEmail,
    LocalizedText Blurb,
    DateTime CreatedAt);

/// <summary>A reviewed provider shown on the manage tab (live or deactivated).</summary>
public record ManagedProviderDto(
    Guid Id,
    int Category,
    string DisplayName,
    string? City,
    string? Phone,
    string? WhatsApp,
    string? ContactEmail,
    LocalizedText Blurb,
    bool IsActive,
    int Priority);

/// <summary>Lead counts for one provider (this month + all time).</summary>
public record LeadSummaryDto(Guid ProviderId, string DisplayName, int MonthCount, int TotalCount);

/// <summary>A single logged lead (a user→provider contact).</summary>
public record RecentLeadDto(Guid ProviderId, string DisplayName, int Category, int Urgency, DateTime CreatedAt);

/// <summary>The admin leads view: per-provider counts plus the most recent leads.</summary>
public record LeadsOverviewDto(IReadOnlyList<LeadSummaryDto> Summary, IReadOnlyList<RecentLeadDto> Recent);

/// <summary>Admin edit of an existing provider. Description fills all three Blurb languages.</summary>
public record UpdateProviderRequest(
    string DisplayName,
    int Category,
    string? City,
    string? Phone,
    string? WhatsApp,
    string? ContactEmail,
    string Description,
    int Priority);
