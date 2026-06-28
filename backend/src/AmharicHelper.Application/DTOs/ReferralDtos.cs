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
