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
public record LogLeadRequest(int? Category, int? Urgency, Guid? DocumentId, string? Ref);

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
    int Priority,
    decimal PricePerLead);

/// <summary>Lead counts and invoice amounts for one provider (this month + all time).
/// MonthAmount/TotalAmount bill every contact tap; BillableMonthAmount/BillableTotalAmount only
/// bill leads marked Converted. HelpfulRate is the share of rated leads the user said helped
/// (null when nothing has been rated yet).</summary>
public record LeadSummaryDto(
    Guid ProviderId, string DisplayName, int MonthCount, int TotalCount,
    decimal PricePerLead, decimal MonthAmount, decimal TotalAmount,
    int ConvertedMonthCount, int ConvertedTotalCount, decimal BillableMonthAmount, decimal BillableTotalAmount,
    double? HelpfulRate);

/// <summary>A single logged lead (a user→provider contact).</summary>
public record RecentLeadDto(
    Guid Id, Guid ProviderId, string DisplayName, int Category, int Urgency, string? Ref,
    int Status, bool? Helpful, DateTime CreatedAt);

/// <summary>Admin edit of a lead's lifecycle status.</summary>
public record UpdateLeadStatusRequest(int Status);

/// <summary>Anonymous post-contact "did this help?" signal, keyed by the lead's Ref code.</summary>
public record LeadFeedbackRequest(bool Helpful);

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
    int Priority,
    decimal PricePerLead);
