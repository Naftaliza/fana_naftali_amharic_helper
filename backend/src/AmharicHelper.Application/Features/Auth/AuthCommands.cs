using System.Security.Cryptography;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AmharicHelper.Application.Features.Auth;

// ---- Register ----
public record RegisterCommand(RegisterRequest Request) : IRequest<Result<RegisterResponse>>;

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
    IOrganizationRepository organizations,
    IPasswordHasher hasher,
    IEmailSender emailSender,
    IConfiguration config,
    IEventTracker events,
    ILogger<RegisterHandler> logger) : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    public async Task<Result<RegisterResponse>> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        if (await users.GetByEmailAsync(req.Email, ct) is not null)
            return Result<RegisterResponse>.Fail("Email already registered.");

        // Registering through a tenant-branded front end tags the new user as that org's
        // member. An unknown/inactive slug is silently ignored — never blocks sign-up.
        Guid? organizationId = null;
        if (!string.IsNullOrWhiteSpace(req.OrganizationSlug))
        {
            var org = await organizations.GetBySlugAsync(req.OrganizationSlug.Trim().ToLowerInvariant(), ct);
            if (org is { IsActive: true }) organizationId = org.Id;
        }

        // Account starts unverified — no JWTs are issued until the emailed link is clicked
        // (see VerifyEmailHandler), same single-use hashed-token pattern as password reset.
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var user = new User
        {
            Email = req.Email,
            DisplayName = req.DisplayName,
            PreferredLanguage = req.PreferredLanguage,
            PasswordHash = hasher.Hash(req.Password),
            OrganizationId = organizationId,
            EmailVerified = false,
            EmailVerificationTokenHash = hasher.Hash(rawToken),
            EmailVerificationExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        await users.AddAsync(user, ct);
        await events.TrackAsync(EventNames.UserRegistered, user.Id, ct);

        var origin = (config["Frontend:Origin"] ?? "http://localhost:3001").Split(',')[0].Trim();
        var link = $"{origin}/verify-email?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(rawToken)}";

        try
        {
            await emailSender.SendAsync(new EmailMessage(
                user.Email,
                "Verify your Fana email address",
                $"Hi {user.DisplayName}, welcome to Fana! Click the link below to verify your email and finish signing up. " +
                $"This link expires in 1 hour and can only be used once.\n\n{link}\n\n" +
                "If you didn't create this account, you can safely ignore this email."), ct);
        }
        catch (Exception ex)
        {
            // Unlike forgot-password, this is safe to surface as a log-only failure without
            // an enumeration risk (the caller already knows it's their own brand-new account) —
            // they can always retry via resend-verification instead of being stuck.
            logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
        }

        return Result<RegisterResponse>.Ok(new RegisterResponse(
            "Account created. Check your email to verify your address and finish signing up.", user.Email));
    }
}

// ---- Login ----
public record LoginCommand(LoginRequest Request) : IRequest<Result<AuthResponse>>;

public class LoginHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IJwtService jwt,
    IRefreshTokenRepository refreshTokens,
    IConfiguration config) : IRequestHandler<LoginCommand, Result<AuthResponse>>
{
    private const string InvalidCredentials = "Invalid email or password.";

    public async Task<Result<AuthResponse>> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var maxAttempts = int.TryParse(config["Auth:MaxFailedLoginAttempts"], out var m) ? m : 5;
        var lockoutMinutes = int.TryParse(config["Auth:LockoutMinutes"], out var l) ? l : 15;

        var user = await users.GetByEmailAsync(cmd.Request.Email, ct);
        if (user is null) return Result<AuthResponse>.Fail(InvalidCredentials);

        // Checked before verifying the password: a still-correct password must not lift an
        // active lockout early. Same generic message as every other failure below — a distinct
        // "account locked" message would let an attacker distinguish a real, locked-out email
        // from a nonexistent one, defeating the anti-enumeration protection this codebase
        // otherwise maintains (see ForgotPasswordHandler/ResetPasswordHandler).
        if (user.LockoutEndsAt is { } until && until > DateTime.UtcNow)
            return Result<AuthResponse>.Fail(InvalidCredentials);

        if (!hasher.Verify(cmd.Request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= maxAttempts)
                user.LockoutEndsAt = DateTime.UtcNow.AddMinutes(lockoutMinutes);
            await users.UpdateAsync(user, ct);
            return Result<AuthResponse>.Fail(InvalidCredentials);
        }

        if (user.FailedLoginAttempts != 0 || user.LockoutEndsAt != null)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutEndsAt = null;
            await users.UpdateAsync(user, ct);
        }

        // Checked only after the password verifies — revealing "unverified" before that would
        // let an attacker distinguish a real unverified email from a nonexistent one on a wrong
        // password. A distinct code (not the generic message) lets the frontend offer a resend.
        if (!user.EmailVerified)
            return Result<AuthResponse>.Fail("EMAIL_NOT_VERIFIED");

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

// ---- Forgot / Reset password ----
public record ForgotPasswordCommand(ForgotPasswordRequest Request) : IRequest<Result<bool>>;

public class ForgotPasswordHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IEmailSender emailSender,
    IConfiguration config,
    ILogger<ForgotPasswordHandler> logger) : IRequestHandler<ForgotPasswordCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ForgotPasswordCommand cmd, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(cmd.Request.Email, ct);
        if (user is not null)
        {
            // Single-use, time-limited token. Only its hash is stored, so a DB leak
            // can't be turned into a password reset.
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.PasswordResetTokenHash = hasher.Hash(rawToken);
            user.PasswordResetExpiresAt = DateTime.UtcNow.AddHours(1);
            await users.UpdateAsync(user, ct);

            // Frontend:Origin already exists for CORS — reuse it rather than adding a new key.
            var origin = (config["Frontend:Origin"] ?? "http://localhost:3001").Split(',')[0].Trim();
            var link = $"{origin}/reset-password?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(rawToken)}";

            try
            {
                await emailSender.SendAsync(new EmailMessage(
                    user.Email,
                    "Reset your Fana password",
                    $"Hi {user.DisplayName}, click the link below to reset your password. " +
                    $"This link expires in 1 hour and can only be used once.\n\n{link}\n\n" +
                    "If you didn't request this, you can safely ignore this email."), ct);
            }
            catch (Exception ex)
            {
                // Never surface a send failure to the caller — same account-enumeration reason
                // this method always returns Ok(true) below. Logged so it's still visible in ops.
                logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
            }
        }

        // Always report success so the endpoint can't be used to enumerate accounts.
        return Result<bool>.Ok(true);
    }
}

public record ResetPasswordCommand(ResetPasswordRequest Request) : IRequest<Result<bool>>;

public class ResetPasswordHandler(IUserRepository users, IPasswordHasher hasher)
    : IRequestHandler<ResetPasswordCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResetPasswordCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 8)
            return Result<bool>.Fail("Password must be at least 8 characters.");

        var user = await users.GetByEmailAsync(req.Email, ct);

        // Reject if there is no pending reset, it has expired, or the token doesn't match.
        // One generic error so callers can't probe which condition failed.
        if (user is null
            || string.IsNullOrEmpty(user.PasswordResetTokenHash)
            || user.PasswordResetExpiresAt is null
            || user.PasswordResetExpiresAt < DateTime.UtcNow
            || string.IsNullOrWhiteSpace(req.ResetToken)
            || !hasher.Verify(req.ResetToken, user.PasswordResetTokenHash))
        {
            return Result<bool>.Fail("Invalid or expired reset request.");
        }

        user.PasswordHash = hasher.Hash(req.NewPassword);
        user.PasswordResetTokenHash = null;   // consume the token (single use)
        user.PasswordResetExpiresAt = null;
        await users.UpdateAsync(user, ct);
        return Result<bool>.Ok(true);
    }
}

// ---- Verify email ----
public record VerifyEmailCommand(VerifyEmailRequest Request) : IRequest<Result<AuthResponse>>;

public class VerifyEmailHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IJwtService jwt,
    IRefreshTokenRepository refreshTokens,
    IEventTracker events) : IRequestHandler<VerifyEmailCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(VerifyEmailCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var user = await users.GetByEmailAsync(req.Email, ct);

        // Same generic-failure shape as ResetPasswordHandler. A second click on an already-used
        // link also lands here, since the token is nulled out on first success below.
        if (user is null
            || string.IsNullOrEmpty(user.EmailVerificationTokenHash)
            || user.EmailVerificationExpiresAt is null
            || user.EmailVerificationExpiresAt < DateTime.UtcNow
            || string.IsNullOrWhiteSpace(req.Token)
            || !hasher.Verify(req.Token, user.EmailVerificationTokenHash))
        {
            return Result<AuthResponse>.Fail("Invalid or expired verification link.");
        }

        user.EmailVerified = true;
        user.EmailVerificationTokenHash = null;   // consume the token (single use)
        user.EmailVerificationExpiresAt = null;
        await users.UpdateAsync(user, ct);
        await events.TrackAsync(EventNames.EmailVerified, user.Id, ct);

        // Verification is the final stage of registration — log the user straight in.
        return Result<AuthResponse>.Ok(await TokenFactory.IssueAsync(user, jwt, refreshTokens, ct));
    }
}

// ---- Resend verification ----
public record ResendVerificationCommand(ResendVerificationRequest Request) : IRequest<Result<bool>>;

public class ResendVerificationHandler(
    IUserRepository users,
    IPasswordHasher hasher,
    IEmailSender emailSender,
    IConfiguration config,
    ILogger<ResendVerificationHandler> logger) : IRequestHandler<ResendVerificationCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(ResendVerificationCommand cmd, CancellationToken ct)
    {
        var user = await users.GetByEmailAsync(cmd.Request.Email, ct);

        // Anti-enumeration, same as ForgotPasswordHandler — also silently no-ops for an
        // already-verified account rather than confirming it exists.
        if (user is not null && !user.EmailVerified)
        {
            var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            user.EmailVerificationTokenHash = hasher.Hash(rawToken);
            user.EmailVerificationExpiresAt = DateTime.UtcNow.AddHours(1);
            await users.UpdateAsync(user, ct);

            var origin = (config["Frontend:Origin"] ?? "http://localhost:3001").Split(',')[0].Trim();
            var link = $"{origin}/verify-email?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(rawToken)}";

            try
            {
                await emailSender.SendAsync(new EmailMessage(
                    user.Email,
                    "Verify your Fana email address",
                    $"Hi {user.DisplayName}, click the link below to verify your email. " +
                    $"This link expires in 1 hour and can only be used once.\n\n{link}\n\n" +
                    "If you didn't request this, you can safely ignore this email."), ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resend verification email to {Email}", user.Email);
            }
        }

        // Always report success so the endpoint can't be used to enumerate accounts.
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
