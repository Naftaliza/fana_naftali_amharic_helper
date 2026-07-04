using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.DTOs;

/// <summary>OrganizationSlug is set when registering through a tenant-branded front end
/// (see Organizations) — an unknown/inactive slug is ignored, not an error.</summary>
public record RegisterRequest(
    string Email, string Password, string DisplayName, Language PreferredLanguage, string? OrganizationSlug = null);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string ResetToken, string NewPassword);

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string Email, string DisplayName, Language PreferredLanguage, bool IsAdmin = false);
