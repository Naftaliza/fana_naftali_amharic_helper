namespace AmharicHelper.Domain.Entities;

/// <summary>
/// A gift of credits from one account to someone else, redeemable via a single-use link — the
/// product answer to "the user and the payer are different people" (see the plan). The sponsor's
/// own balance is debited when the gift is created (see WalletService.TryDebitForSponsorshipAsync),
/// not when it's redeemed, so a sponsor can never gift credits they don't have. RedeemTokenHash is
/// a plain SHA-256 of the raw token (not the PBKDF2 IPasswordHasher used for account passwords) —
/// unlike a password, the token is 256 bits of random entropy the user never chooses, so a
/// deterministic hash is safe and, unlike PBKDF2, lets redemption look the row up directly by
/// token instead of needing a second identifier (e.g. an email) in the link.
/// </summary>
public class Sponsorship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SponsorUserId { get; set; }

    /// <summary>SHA-256 of the phone number the sponsor said this was for — informational only
    /// (shown back to the sponsor in their own gift history); redemption is gated purely by
    /// possession of the link, not by matching this to the redeemer.</summary>
    public string BeneficiaryContactHash { get; set; } = string.Empty;

    public int Credits { get; set; }
    public string RedeemTokenHash { get; set; } = string.Empty;

    public Guid? RedeemedByUserId { get; set; }
    public DateTime? RedeemedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
