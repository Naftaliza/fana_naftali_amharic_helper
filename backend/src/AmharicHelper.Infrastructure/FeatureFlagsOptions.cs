namespace AmharicHelper.Infrastructure;

/// <summary>
/// Server-side feature switches, bound from the "Features" config section (env
/// Features__Wallet, etc.). Wallet mirrors the frontend's NEXT_PUBLIC_FEATURE_WALLET so the
/// two never drift: off means WalletService.TryConsumeAsync never blocks a request (today's
/// production behavior — unlimited for signed-in users, client-only counting for anonymous
/// trials), on means it actually meters and enforces credits server-side.
/// </summary>
public class FeatureFlagsOptions
{
    public bool Wallet { get; set; }
}
