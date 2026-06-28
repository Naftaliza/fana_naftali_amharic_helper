using AmharicHelper.Domain.Enums;

namespace AmharicHelper.Application.DTOs;

public record RegisterRequest(string Email, string Password, string DisplayName, Language PreferredLanguage);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string ResetToken, string NewPassword);

public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

public record UserDto(Guid Id, string Email, string DisplayName, Language PreferredLanguage, bool IsAdmin = false);
