using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Auth;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AmharicHelper.UnitTests;

public class AuthCommandsTests
{
    // ---- Test doubles (this project has no mocking library — see UploadDocumentHandlerTests) ----

    private sealed class FakeUserRepository : IUserRepository
    {
        public User? ToReturn { get; set; }
        public User? Added { get; private set; }
        public User? Updated { get; private set; }
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task AddAsync(User user, CancellationToken ct = default) { Added = user; return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken ct = default) { Updated = user; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public bool ThrowOnSend { get; set; }
        public List<EmailMessage> Sent { get; } = new();
        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (ThrowOnSend) throw new InvalidOperationException("SMTP connection failed.");
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static IConfiguration Config() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Frontend:Origin"] = "http://localhost:3001" }).Build();

    private static IConfiguration LoginConfig(int maxAttempts = 5, int lockoutMinutes = 15) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:MaxFailedLoginAttempts"] = maxAttempts.ToString(),
            ["Auth:LockoutMinutes"] = lockoutMinutes.ToString(),
        }).Build();

    private sealed class FakeJwtService : IJwtService
    {
        public string CreateAccessToken(User user) => "fake-access-token";
        public string CreateRefreshToken() => "fake-refresh-token";
    }

    private sealed class FakeOrganizationRepository : IOrganizationRepository
    {
        public Task<Organization?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Organization?>(null);
        public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Organization?>(null);
        public Task<IReadOnlyList<Organization>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Organization>>(new List<Organization>());
        public Task AddAsync(Organization organization, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Organization organization, CancellationToken ct = default) => Task.CompletedTask;
        public Task SetActiveAsync(Guid id, bool active, CancellationToken ct = default) => Task.CompletedTask;
        public Task<OrganizationStatsDto> GetStatsAsync(Guid organizationId, CancellationToken ct = default) => Task.FromResult<OrganizationStatsDto>(null!);
    }

    private sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
    {
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default) => Task.FromResult<RefreshToken?>(null);
        public Task AddAsync(RefreshToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task RevokeAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeEventTracker : IEventTracker
    {
        public List<(string Name, Guid? UserId)> Tracked { get; } = new();
        public Task TrackAsync(string eventName, Guid? userId = null, CancellationToken ct = default)
        {
            Tracked.Add((eventName, userId));
            return Task.CompletedTask;
        }
    }

    // Reuse the real hasher, matching PasswordHasherTests.cs's existing convention.
    private static readonly PasswordHasher Hasher = new();

    // ---- RegisterHandler ----

    [Fact]
    public async Task Register_creates_an_unverified_user_and_sends_a_verification_email_without_issuing_tokens()
    {
        var users = new FakeUserRepository { ToReturn = null };
        var emailSender = new FakeEmailSender();
        var handler = new RegisterHandler(users, new FakeOrganizationRepository(), Hasher, emailSender, Config(), new FakeEventTracker(), NullLogger<RegisterHandler>.Instance);

        var result = await handler.Handle(
            new RegisterCommand(new RegisterRequest("new@test.local", "password123", "New User", AmharicHelper.Domain.Enums.Language.Hebrew)), default);

        Assert.True(result.Success);
        Assert.Equal("new@test.local", result.Value!.Email);

        Assert.NotNull(users.Added);
        Assert.False(users.Added!.EmailVerified);
        Assert.NotNull(users.Added.EmailVerificationTokenHash);
        Assert.NotNull(users.Added.EmailVerificationExpiresAt);

        Assert.Single(emailSender.Sent);
        Assert.Equal("new@test.local", emailSender.Sent[0].ToEmail);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_verified_email()
    {
        var users = new FakeUserRepository { ToReturn = new User { Email = "new@test.local", EmailVerified = true } };
        var handler = new RegisterHandler(users, new FakeOrganizationRepository(), Hasher, new FakeEmailSender(), Config(), new FakeEventTracker(), NullLogger<RegisterHandler>.Instance);

        var result = await handler.Handle(
            new RegisterCommand(new RegisterRequest("new@test.local", "password123", "New User", AmharicHelper.Domain.Enums.Language.Hebrew)), default);

        Assert.False(result.Success);
        Assert.Null(users.Added);
        Assert.Null(users.Updated);
    }

    [Fact]
    public async Task Register_reissues_an_abandoned_unverified_registration_for_the_same_email()
    {
        // A never-verified account (e.g. a mistyped/unreachable address, or a lost verification
        // email) previously left that address permanently stuck — "Email already registered"
        // forever, with no way to correct it and try again. Re-registering it should overwrite
        // the abandoned row instead of blocking it.
        var users = new FakeUserRepository { ToReturn = new User { Email = "new@test.local", DisplayName = "Old Name", EmailVerified = false } };
        var emailSender = new FakeEmailSender();
        var handler = new RegisterHandler(users, new FakeOrganizationRepository(), Hasher, emailSender, Config(), new FakeEventTracker(), NullLogger<RegisterHandler>.Instance);

        var result = await handler.Handle(
            new RegisterCommand(new RegisterRequest("new@test.local", "password123", "New Name", AmharicHelper.Domain.Enums.Language.Hebrew)), default);

        Assert.True(result.Success);
        Assert.Null(users.Added);
        Assert.NotNull(users.Updated);
        Assert.Equal("New Name", users.Updated!.DisplayName);
        Assert.False(users.Updated.EmailVerified);
        Assert.NotNull(users.Updated.EmailVerificationTokenHash);
        Assert.Single(emailSender.Sent);
    }

    // ---- LoginHandler ----

    [Fact]
    public async Task Login_rejects_an_unverified_account()
    {
        var user = new User { Email = "user@test.local", PasswordHash = Hasher.Hash("correct-password"), EmailVerified = false };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new LoginHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), LoginConfig());

        var result = await handler.Handle(new LoginCommand(new LoginRequest("user@test.local", "correct-password")), default);

        Assert.False(result.Success);
        Assert.Equal("EMAIL_NOT_VERIFIED", result.Error);
    }

    [Fact]
    public async Task Login_succeeds_and_resets_prior_failed_attempts()
    {
        var user = new User
        {
            Email = "user@test.local",
            PasswordHash = Hasher.Hash("correct-password"),
            FailedLoginAttempts = 3,
            LockoutEndsAt = null,
            EmailVerified = true,
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new LoginHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), LoginConfig());

        var result = await handler.Handle(new LoginCommand(new LoginRequest("user@test.local", "correct-password")), default);

        Assert.True(result.Success);
        Assert.NotNull(users.Updated);
        Assert.Equal(0, users.Updated!.FailedLoginAttempts);
        Assert.Null(users.Updated.LockoutEndsAt);
    }

    [Fact]
    public async Task Login_wrong_password_increments_failed_attempts_without_locking()
    {
        var user = new User { Email = "user@test.local", PasswordHash = Hasher.Hash("correct-password"), EmailVerified = true };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new LoginHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), LoginConfig(maxAttempts: 5));

        var result = await handler.Handle(new LoginCommand(new LoginRequest("user@test.local", "wrong-password")), default);

        Assert.False(result.Success);
        Assert.NotNull(users.Updated);
        Assert.Equal(1, users.Updated!.FailedLoginAttempts);
        Assert.Null(users.Updated.LockoutEndsAt);
    }

    [Fact]
    public async Task Login_reaching_the_attempt_threshold_locks_the_account()
    {
        var user = new User { Email = "user@test.local", PasswordHash = Hasher.Hash("correct-password"), FailedLoginAttempts = 4, EmailVerified = true };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new LoginHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), LoginConfig(maxAttempts: 5, lockoutMinutes: 15));

        var result = await handler.Handle(new LoginCommand(new LoginRequest("user@test.local", "wrong-password")), default);

        Assert.False(result.Success);
        Assert.NotNull(users.Updated);
        Assert.Equal(5, users.Updated!.FailedLoginAttempts);
        Assert.NotNull(users.Updated.LockoutEndsAt);
        Assert.True(users.Updated.LockoutEndsAt > DateTime.UtcNow.AddMinutes(10));
        Assert.True(users.Updated.LockoutEndsAt < DateTime.UtcNow.AddMinutes(20));
    }

    [Fact]
    public async Task Login_rejects_a_locked_account_even_with_the_correct_password()
    {
        var user = new User
        {
            Email = "user@test.local",
            PasswordHash = Hasher.Hash("correct-password"),
            FailedLoginAttempts = 5,
            LockoutEndsAt = DateTime.UtcNow.AddMinutes(10),
            EmailVerified = true,
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new LoginHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), LoginConfig());

        var result = await handler.Handle(new LoginCommand(new LoginRequest("user@test.local", "correct-password")), default);

        Assert.False(result.Success);
        // The lockout branch returns before ever touching the repository — it must not reset
        // or otherwise mutate the attempt counters while still locked.
        Assert.Null(users.Updated);
    }

    [Fact]
    public async Task Login_succeeds_once_the_lockout_has_expired()
    {
        var user = new User
        {
            Email = "user@test.local",
            PasswordHash = Hasher.Hash("correct-password"),
            FailedLoginAttempts = 5,
            LockoutEndsAt = DateTime.UtcNow.AddMinutes(-1),
            EmailVerified = true,
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new LoginHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), LoginConfig());

        var result = await handler.Handle(new LoginCommand(new LoginRequest("user@test.local", "correct-password")), default);

        Assert.True(result.Success);
        Assert.NotNull(users.Updated);
        Assert.Equal(0, users.Updated!.FailedLoginAttempts);
        Assert.Null(users.Updated.LockoutEndsAt);
    }

    // ---- ForgotPasswordHandler ----

    [Fact]
    public async Task ForgotPassword_unknown_email_sends_nothing_but_still_reports_success()
    {
        var users = new FakeUserRepository { ToReturn = null };
        var emailSender = new FakeEmailSender();
        var handler = new ForgotPasswordHandler(users, Hasher, emailSender, Config(), NullLogger<ForgotPasswordHandler>.Instance);

        var result = await handler.Handle(new ForgotPasswordCommand(new ForgotPasswordRequest("nobody@test.local")), default);

        // Account-enumeration protection: the caller can never distinguish "no such account"
        // from "email sent" — this is the entire reason the handler always returns Ok(true).
        Assert.True(result.Success);
        Assert.True(result.Value);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task ForgotPassword_known_email_sends_exactly_one_email_with_a_hashed_stored_token()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.local", DisplayName = "User" };
        var users = new FakeUserRepository { ToReturn = user };
        var emailSender = new FakeEmailSender();
        var handler = new ForgotPasswordHandler(users, Hasher, emailSender, Config(), NullLogger<ForgotPasswordHandler>.Instance);

        var result = await handler.Handle(new ForgotPasswordCommand(new ForgotPasswordRequest("user@test.local")), default);

        Assert.True(result.Success);
        Assert.Single(emailSender.Sent);
        Assert.Equal("user@test.local", emailSender.Sent[0].ToEmail);

        Assert.NotNull(users.Updated);
        Assert.NotNull(users.Updated!.PasswordResetTokenHash);
        Assert.NotNull(users.Updated.PasswordResetExpiresAt);
        Assert.True(users.Updated.PasswordResetExpiresAt > DateTime.UtcNow.AddMinutes(50));
        Assert.True(users.Updated.PasswordResetExpiresAt < DateTime.UtcNow.AddMinutes(70));

        // The raw token appears in the emailed link but must never equal the stored hash —
        // confirms it's actually hashed, not stored raw.
        var link = emailSender.Sent[0].Body;
        var rawToken = link[(link.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..].Split('\n')[0].Trim();
        Assert.NotEqual(rawToken, users.Updated.PasswordResetTokenHash);
        Assert.True(Hasher.Verify(rawToken, users.Updated.PasswordResetTokenHash!));
    }

    [Fact]
    public async Task ForgotPassword_still_succeeds_when_email_send_throws()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@test.local", DisplayName = "User" };
        var users = new FakeUserRepository { ToReturn = user };
        var emailSender = new FakeEmailSender { ThrowOnSend = true };
        var handler = new ForgotPasswordHandler(users, Hasher, emailSender, Config(), NullLogger<ForgotPasswordHandler>.Instance);

        var result = await handler.Handle(new ForgotPasswordCommand(new ForgotPasswordRequest("user@test.local")), default);

        // Same account-enumeration reasoning as the unknown-email case: a send failure must
        // never surface differently than success.
        Assert.True(result.Success);
        Assert.True(result.Value);
    }

    // ---- ResetPasswordHandler ----

    [Fact]
    public async Task ResetPassword_fails_when_no_pending_reset()
    {
        var user = new User { Email = "user@test.local", PasswordResetTokenHash = null, PasswordResetExpiresAt = null };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new ResetPasswordHandler(users, Hasher);

        var result = await handler.Handle(new ResetPasswordCommand(new ResetPasswordRequest("user@test.local", "sometoken", "newpassword123")), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ResetPassword_fails_when_token_expired()
    {
        var rawToken = "raw-reset-token";
        var user = new User
        {
            Email = "user@test.local",
            PasswordResetTokenHash = Hasher.Hash(rawToken),
            PasswordResetExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new ResetPasswordHandler(users, Hasher);

        var result = await handler.Handle(new ResetPasswordCommand(new ResetPasswordRequest("user@test.local", rawToken, "newpassword123")), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ResetPassword_fails_for_wrong_token()
    {
        var user = new User
        {
            Email = "user@test.local",
            PasswordResetTokenHash = Hasher.Hash("the-real-token"),
            PasswordResetExpiresAt = DateTime.UtcNow.AddHours(1),
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new ResetPasswordHandler(users, Hasher);

        var result = await handler.Handle(new ResetPasswordCommand(new ResetPasswordRequest("user@test.local", "a-different-token", "newpassword123")), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ResetPassword_succeeds_and_consumes_the_token()
    {
        var rawToken = "the-real-token";
        var user = new User
        {
            Email = "user@test.local",
            PasswordHash = Hasher.Hash("old-password"),
            PasswordResetTokenHash = Hasher.Hash(rawToken),
            PasswordResetExpiresAt = DateTime.UtcNow.AddHours(1),
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new ResetPasswordHandler(users, Hasher);

        var result = await handler.Handle(new ResetPasswordCommand(new ResetPasswordRequest("user@test.local", rawToken, "newpassword123")), default);

        Assert.True(result.Success);
        Assert.NotNull(users.Updated);
        Assert.True(Hasher.Verify("newpassword123", users.Updated!.PasswordHash));
        // Single-use: the token must be consumed so the same link can never be replayed.
        Assert.Null(users.Updated.PasswordResetTokenHash);
        Assert.Null(users.Updated.PasswordResetExpiresAt);
    }

    // ---- VerifyEmailHandler ----

    [Fact]
    public async Task VerifyEmail_succeeds_and_issues_tokens_for_a_valid_token()
    {
        var rawToken = "the-real-token";
        var user = new User
        {
            Email = "user@test.local",
            EmailVerified = false,
            EmailVerificationTokenHash = Hasher.Hash(rawToken),
            EmailVerificationExpiresAt = DateTime.UtcNow.AddHours(1),
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new VerifyEmailHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), new FakeEventTracker());

        var result = await handler.Handle(new VerifyEmailCommand(new VerifyEmailRequest("user@test.local", rawToken)), default);

        Assert.True(result.Success);
        Assert.NotNull(result.Value!.AccessToken);
        Assert.NotNull(users.Updated);
        Assert.True(users.Updated!.EmailVerified);
        // Single-use: the token must be consumed so the same link can never be replayed.
        Assert.Null(users.Updated.EmailVerificationTokenHash);
        Assert.Null(users.Updated.EmailVerificationExpiresAt);
    }

    [Fact]
    public async Task VerifyEmail_fails_for_expired_token()
    {
        var rawToken = "the-real-token";
        var user = new User
        {
            Email = "user@test.local",
            EmailVerified = false,
            EmailVerificationTokenHash = Hasher.Hash(rawToken),
            EmailVerificationExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new VerifyEmailHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), new FakeEventTracker());

        var result = await handler.Handle(new VerifyEmailCommand(new VerifyEmailRequest("user@test.local", rawToken)), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task VerifyEmail_fails_for_wrong_token()
    {
        var user = new User
        {
            Email = "user@test.local",
            EmailVerified = false,
            EmailVerificationTokenHash = Hasher.Hash("the-real-token"),
            EmailVerificationExpiresAt = DateTime.UtcNow.AddHours(1),
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new VerifyEmailHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), new FakeEventTracker());

        var result = await handler.Handle(new VerifyEmailCommand(new VerifyEmailRequest("user@test.local", "a-different-token")), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task VerifyEmail_fails_when_already_verified_and_token_consumed()
    {
        var user = new User
        {
            Email = "user@test.local",
            EmailVerified = true,
            EmailVerificationTokenHash = null,
            EmailVerificationExpiresAt = null,
        };
        var users = new FakeUserRepository { ToReturn = user };
        var handler = new VerifyEmailHandler(users, Hasher, new FakeJwtService(), new FakeRefreshTokenRepository(), new FakeEventTracker());

        var result = await handler.Handle(new VerifyEmailCommand(new VerifyEmailRequest("user@test.local", "anything")), default);

        Assert.False(result.Success);
    }

    // ---- ResendVerificationHandler ----

    [Fact]
    public async Task ResendVerification_unknown_email_sends_nothing_but_still_reports_success()
    {
        var users = new FakeUserRepository { ToReturn = null };
        var emailSender = new FakeEmailSender();
        var handler = new ResendVerificationHandler(users, Hasher, emailSender, Config(), NullLogger<ResendVerificationHandler>.Instance);

        var result = await handler.Handle(new ResendVerificationCommand(new ResendVerificationRequest("nobody@test.local")), default);

        Assert.True(result.Success);
        Assert.Empty(emailSender.Sent);
    }

    [Fact]
    public async Task ResendVerification_already_verified_email_sends_nothing_but_still_reports_success()
    {
        var user = new User { Email = "user@test.local", EmailVerified = true };
        var users = new FakeUserRepository { ToReturn = user };
        var emailSender = new FakeEmailSender();
        var handler = new ResendVerificationHandler(users, Hasher, emailSender, Config(), NullLogger<ResendVerificationHandler>.Instance);

        var result = await handler.Handle(new ResendVerificationCommand(new ResendVerificationRequest("user@test.local")), default);

        Assert.True(result.Success);
        Assert.Empty(emailSender.Sent);
        Assert.Null(users.Updated);
    }

    [Fact]
    public async Task ResendVerification_known_unverified_email_sends_exactly_one_email_with_a_hashed_stored_token()
    {
        var user = new User { Email = "user@test.local", DisplayName = "User", EmailVerified = false };
        var users = new FakeUserRepository { ToReturn = user };
        var emailSender = new FakeEmailSender();
        var handler = new ResendVerificationHandler(users, Hasher, emailSender, Config(), NullLogger<ResendVerificationHandler>.Instance);

        var result = await handler.Handle(new ResendVerificationCommand(new ResendVerificationRequest("user@test.local")), default);

        Assert.True(result.Success);
        Assert.Single(emailSender.Sent);
        Assert.NotNull(users.Updated);
        Assert.NotNull(users.Updated!.EmailVerificationTokenHash);
        Assert.NotNull(users.Updated.EmailVerificationExpiresAt);
    }
}
