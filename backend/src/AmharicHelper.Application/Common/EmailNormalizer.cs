namespace AmharicHelper.Application.Common;

/// <summary>
/// Emails are looked up via an exact, case-sensitive match against a case-sensitive unique
/// index (see UX_Users_Email), so registration and every lookup must agree on one casing —
/// otherwise the same address typed with different capitalization silently becomes a
/// "different" user and login fails with the generic invalid-credentials error.
/// </summary>
public static class EmailNormalizer
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
