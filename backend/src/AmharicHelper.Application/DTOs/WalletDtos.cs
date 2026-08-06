namespace AmharicHelper.Application.DTOs;

/// <summary>The credit balance and its free-tier context for one subject.</summary>
public record WalletBalanceDto(int Balance, int FreeTierCredits);

public record UsageLedgerEntryDto(
    DateTime CreatedAt, int Kind, int Delta, string Operation, string Note, Guid? DocumentId);

/// <summary>My wallet page: current balance plus recent movements.</summary>
public record WalletSummaryDto(WalletBalanceDto Balance, IReadOnlyList<UsageLedgerEntryDto> History);

/// <summary>Admin: grant credits to a subject identified either by an existing user's email or
/// by a raw phone number (hashed the same way a future WhatsApp identity would be).</summary>
public record AdminGrantCreditsRequest(string? Email, string? Phone, int Credits, string Note);

// ---- Sponsorship: someone else pays (see Sponsorship entity) ----

/// <summary>Create a gift. Phone is stored only as a hash, purely informational for the
/// sponsor's own history — redemption is gated by the link, not by matching this.</summary>
public record CreateSponsorshipRequest(string BeneficiaryPhone, int Credits);

/// <summary>The raw redeem token is returned exactly once, at creation — only its hash is ever
/// persisted (see Sponsorship.RedeemTokenHash), so this is the only response that can build the
/// shareable link.</summary>
public record CreateSponsorshipResponse(Guid Id, string RedeemToken, int Credits, DateTime ExpiresAt);

public record SponsorshipSummaryDto(Guid Id, int Credits, bool Redeemed, DateTime CreatedAt, DateTime ExpiresAt);

public record RedeemSponsorshipResponseDto(int CreditsGranted, int NewBalance);
