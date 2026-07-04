using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Domain.Entities;

/// <summary>A registered user of Amharic Helper.</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Language PreferredLanguage { get; set; } = Language.Hebrew;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set when this user belongs to a B2B/B2G tenant (see <see cref="Organization"/>);
    /// null for ordinary consumer sign-ups.</summary>
    public Guid? OrganizationId { get; set; }

    // Single-use, time-limited password reset. Only the token hash is persisted.
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiresAt { get; set; }
}
