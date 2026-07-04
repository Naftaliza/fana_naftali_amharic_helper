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
        public User? Updated { get; private set; }
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(ToReturn);
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken ct = default) { Updated = user; return Task.CompletedTask; }
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

    // Reuse the real hasher, matching PasswordHasherTests.cs's existing convention.
    private static readonly PasswordHasher Hasher = new();

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
}
