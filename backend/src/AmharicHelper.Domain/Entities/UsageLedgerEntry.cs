using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Domain.Entities;

/// <summary>
/// One append-only movement in a subject's credit balance (see UsageSubject in the Application
/// layer). A subject's balance is always SUM(Delta) over their rows — there is deliberately no
/// mutable balance column, so a double-spend bug can't silently corrupt state and every charge
/// is individually auditable. Written only by WalletService, inside a transaction serialized per
/// subject via a Postgres advisory lock (see WalletService.TryConsumeAsync).
/// </summary>
public class UsageLedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string? ContactHash { get; set; }

    public UsageLedgerKind Kind { get; set; }

    /// <summary>Signed credit movement — positive for Grant/Refund/Sponsorship, negative for
    /// Consume/Expire.</summary>
    public int Delta { get; set; }

    /// <summary>What the credit paid for: "analyze" | "tts" | "chat" | "free_tier" (an automatic
    /// one-time grant, not a real spend — "free_tier_monthly" is the same thing under the old,
    /// pre-one-time operation name, kept only so historical rows still read sensibly).</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Observed provider cost in USD for this movement, when known — the first place
    /// this project can answer what a user actually costs. Null until wired to real token counts.</summary>
    public decimal? CostUsd { get; set; }

    public Guid? DocumentId { get; set; }
    public string Note { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
