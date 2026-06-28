namespace AmharicHelper.Application.Common;

/// <summary>
/// Lightweight admin gate: an email is an admin if it appears in the comma-separated
/// "Admin:Emails" configuration value. Avoids a roles/permissions schema for now.
/// </summary>
public static class AdminPolicy
{
    public static bool IsAdmin(string? email, string? adminEmailsCsv)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(adminEmailsCsv))
            return false;

        return adminEmailsCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase));
    }
}
