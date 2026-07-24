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
public record VerifyEmailRequest(string Email, string Token);
public record ResendVerificationRequest(string Email);

/// <summary>Returned by registration now that no tokens are issued immediately — the caller
/// must wait for the emailed verification link (see VerifyEmailRequest).</summary>
public record RegisterResponse(string Message, string Email);

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string Email, string DisplayName, Language PreferredLanguage, bool IsAdmin = false);
