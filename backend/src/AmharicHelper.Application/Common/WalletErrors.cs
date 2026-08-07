namespace AmharicHelper.Application.Common;

/// <summary>Stable error codes a metered handler returns via Result&lt;T&gt;.Fail so the frontend
/// can map them to a localized prompt (same pattern as AuthCommands' EMAIL_NOT_VERIFIED) instead
/// of showing a raw server message.</summary>
public static class WalletErrors
{
    public const string OutOfCredits = "OUT_OF_CREDITS";
}
