using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using FluentValidation;
using MediatR;

namespace AmharicHelper.Application.Features.Auth;

// ---- Register ----
public record RegisterCommand(RegisterRequest Request) : IRequest<Result<AuthResponse>>;

public class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Request.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Request.Password).MinimumLength(8);
        RuleFor(x => x.Request.DisplayName).NotEmpty();
    }
}

public class RegisterHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IJwtService jwt,
    IRefreshTokenRepository refreshTokens) : IRequestHandler<RegisterCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        if (await users.GetByEmailAsync(req.Email, ct) is not null)
            return Result<AuthResponse>.Fail("Email already registered.");

        var user = new User
        {
            Email = req.Email,
            DisplayName = req.DisplayName,
            PreferredLanguage = req.PreferredLanguage,
            PasswordHash = hasher.Hash(req.Password)
        };
        await users.AddAsync(user, ct);

        return Result<AuthResponse>.Ok(await TokenFactory.IssueAsync(user, jwt, refreshTokens, ct));
    }
}

// ---- Login ----
public record LoginCommand(LoginRequest Request) : IRequest<Result<AuthResponse>>;

public class LoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IJwtService jwt,
    IRefreshTokenRepository refreshTokens) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(cmd.Request.Email, ct);
        if (user is null || !hasher.Verify(cmd.Request.Password, user.PasswordHash))
            return Result<AuthResponse>.Fail("Invalid email or password.");

        return Result<AuthResponse>.Ok(await TokenFactory.IssueAsync(user, jwt, refreshTokens, ct));
    }
}

// ---- Refresh ----
public record RefreshCommand(RefreshRequest Request) : IRequest<Result<AuthResponse>>;

public class RefreshHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IJwtService jwt) : IRequestHandler<RefreshCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(RefreshCommand cmd, CancellationToken ct)
    {
        var existing = await refreshTokens.GetByTokenAsync(cmd.Request.RefreshToken, ct);
        if (existing is null || !existing.IsActive)
            return Result<AuthResponse>.Fail("Invalid or expired refresh token.");

        var user = await users.GetByIdAsync(existing.UserId, ct);
        if (user is null) return Result<AuthResponse>.Fail("User not found.");

        await refreshTokens.RevokeAsync(existing.Id, ct);
        return Result<AuthResponse>.Ok(await TokenFactory.IssueAsync(user, jwt, refreshTokens, ct));
    }
}

// ---- Forgot / Reset password (stubbed: email delivery is a follow-up) ----
public record ForgotPasswordCommand(ForgotPasswordRequest Request) : IRequest<Result<bool>>;

public class ForgotPasswordHandler(IUserRepository users) : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        // MVP scaffold: always return success to avoid leaking which emails exist.
        // TODO: generate a time-limited reset token and email it to the user.
        _ = await users.GetByEmailAsync(cmd.Request.Email, ct);
        return Result<bool>.Ok(true);
    }
}

public record ResetPasswordCommand(ResetPasswordRequest Request) : IRequest<Result<bool>>;

public class ResetPasswordHandler(IUserRepository users, IPasswordHasher hasher)
    : IRequestHandler<ResetPasswordCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        // TODO: validate the reset token before allowing the change.
        var user = await users.GetByEmailAsync(cmd.Request.Email, ct);
        if (user is null) return Result<bool>.Fail("Invalid reset request.");
        user.PasswordHash = hasher.Hash(cmd.Request.NewPassword);
        await users.UpdateAsync(user, ct);
        return Result<bool>.Ok(true);
    }
}

internal static class TokenFactory
{
    public static async Task<AuthResponse> IssueAsync(
        User user, IJwtService jwt, IRefreshTokenRepository refreshTokens, CancellationToken ct)
    {
        var access = jwt.CreateAccessToken(user);
        var refresh = jwt.CreateRefreshToken();
        await refreshTokens.AddAsync(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        }, ct);

        return new AuthResponse(access, refresh,
            new UserDto(user.Id, user.Email, user.DisplayName, user.PreferredLanguage));
    }
}
