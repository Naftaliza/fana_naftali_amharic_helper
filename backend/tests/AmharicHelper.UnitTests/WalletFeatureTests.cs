using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Wallet;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AmharicHelper.UnitTests;

public class WalletFeatureTests
{
    // ---- Test doubles (this project has no mocking library — see UploadDocumentHandlerTests) ----

    private sealed class FakeWallet : IWalletService
    {
        public int BalanceToReturn { get; set; }
        public bool AllowDebit { get; set; } = true;
        public IReadOnlyList<UsageLedgerEntry> HistoryToReturn { get; set; } = Array.Empty<UsageLedgerEntry>();
        public List<(UsageSubject Subject, int Credits, UsageLedgerKind Kind, string Note)> Grants { get; } = new();
        public List<(UsageSubject Subject, int Credits, string Note)> Debits { get; } = new();
        public List<(UsageSubject Subject, int Credits, string Note)> SponsorshipGrants { get; } = new();

        public Task<int> GetBalanceAsync(UsageSubject subject, CancellationToken ct = default) => Task.FromResult(BalanceToReturn);
        public Task<bool> TryConsumeAsync(UsageSubject subject, string operation, Guid? documentId = null, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task GrantAsync(UsageSubject subject, int credits, UsageLedgerKind kind, string note, CancellationToken ct = default)
        {
            Grants.Add((subject, credits, kind, note));
            return Task.CompletedTask;
        }
        public Task<bool> TryDebitForSponsorshipAsync(UsageSubject subject, int credits, string note, CancellationToken ct = default)
        {
            if (AllowDebit) Debits.Add((subject, credits, note));
            return Task.FromResult(AllowDebit);
        }
        public Task GrantSponsorshipAsync(UsageSubject subject, int credits, string note, CancellationToken ct = default)
        {
            SponsorshipGrants.Add((subject, credits, note));
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<UsageLedgerEntry>> GetHistoryAsync(UsageSubject subject, int limit, CancellationToken ct = default) =>
            Task.FromResult(HistoryToReturn);
    }

    private sealed class FakeUsers : IUserRepository
    {
        public User? ToReturnById { get; set; }
        public User? ToReturnByEmail { get; set; }
        public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToReturnById);
        public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(ToReturnByEmail);
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static IConfiguration Config(string adminEmails = "admin@test.local") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Admin:Emails"] = adminEmails }).Build();

    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid NonAdminUserId = Guid.NewGuid();

    private static FakeUsers AdminUsers() => new() { ToReturnById = new User { Id = AdminUserId, Email = "admin@test.local" } };

    // ---- GetWalletHandler ----

    [Fact]
    public async Task Returns_balance_and_the_monthly_free_tier_constant()
    {
        var wallet = new FakeWallet { BalanceToReturn = 2 };
        var handler = new GetWalletHandler(wallet);

        var result = await handler.Handle(new GetWalletQuery(Guid.NewGuid()), default);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Balance.Balance);
        Assert.Equal(WalletDefaults.FreeTierCredits, result.Value.Balance.FreeTierCredits);
    }

    [Fact]
    public async Task Maps_ledger_history_into_dtos()
    {
        var wallet = new FakeWallet
        {
            HistoryToReturn = new[]
            {
                new UsageLedgerEntry { Kind = UsageLedgerKind.Consume, Delta = -1, Operation = "analyze", CreatedAt = DateTime.UtcNow }
            }
        };
        var handler = new GetWalletHandler(wallet);

        var result = await handler.Handle(new GetWalletQuery(Guid.NewGuid()), default);

        Assert.True(result.Success);
        Assert.Single(result.Value!.History);
        Assert.Equal("analyze", result.Value.History[0].Operation);
        Assert.Equal(-1, result.Value.History[0].Delta);
    }

    // ---- AdminGrantCreditsHandler ----

    [Fact]
    public async Task Grant_by_non_admin_is_forbidden()
    {
        var wallet = new FakeWallet();
        var users = new FakeUsers { ToReturnById = new User { Id = NonAdminUserId, Email = "nobody@test.local" } };
        var handler = new AdminGrantCreditsHandler(wallet, users, Config());

        var result = await handler.Handle(
            new AdminGrantCreditsCommand(NonAdminUserId, new AdminGrantCreditsRequest("someone@test.local", null, 5, "")), default);

        Assert.False(result.Success);
        Assert.Equal("Forbidden", result.Error);
        Assert.Empty(wallet.Grants);
    }

    [Fact]
    public async Task Grant_rejects_non_positive_credits()
    {
        var handler = new AdminGrantCreditsHandler(new FakeWallet(), AdminUsers(), Config());

        var result = await handler.Handle(
            new AdminGrantCreditsCommand(AdminUserId, new AdminGrantCreditsRequest("someone@test.local", null, 0, "")), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Grant_by_unknown_email_fails()
    {
        var users = AdminUsers();
        users.ToReturnByEmail = null;
        var handler = new AdminGrantCreditsHandler(new FakeWallet(), users, Config());

        var result = await handler.Handle(
            new AdminGrantCreditsCommand(AdminUserId, new AdminGrantCreditsRequest("ghost@test.local", null, 5, "")), default);

        Assert.False(result.Success);
        Assert.Equal("No account with that email.", result.Error);
    }

    [Fact]
    public async Task Grant_by_email_resolves_the_account_and_grants_credits()
    {
        var target = new User { Id = Guid.NewGuid(), Email = "member@test.local" };
        var users = AdminUsers();
        users.ToReturnByEmail = target;
        var wallet = new FakeWallet { BalanceToReturn = 5 };
        var handler = new AdminGrantCreditsHandler(wallet, users, Config());

        var result = await handler.Handle(
            new AdminGrantCreditsCommand(AdminUserId, new AdminGrantCreditsRequest("Member@Test.Local", null, 5, "Paid by Bit")), default);

        Assert.True(result.Success);
        Assert.Equal(5, result.Value);
        Assert.Single(wallet.Grants);
        Assert.Equal(UsageSubject.ForUser(target.Id), wallet.Grants[0].Subject);
        Assert.Equal(5, wallet.Grants[0].Credits);
        Assert.Equal("Paid by Bit", wallet.Grants[0].Note);
    }

    [Fact]
    public async Task Grant_by_phone_hashes_the_number_as_the_subject()
    {
        var wallet = new FakeWallet();
        var handler = new AdminGrantCreditsHandler(wallet, AdminUsers(), Config());

        var result = await handler.Handle(
            new AdminGrantCreditsCommand(AdminUserId, new AdminGrantCreditsRequest(null, "+972500000000", 3, "")), default);

        Assert.True(result.Success);
        Assert.Equal(UsageSubject.ForPhone("+972500000000"), wallet.Grants[0].Subject);
        Assert.Null(wallet.Grants[0].Subject.UserId);
    }

    [Fact]
    public async Task Grant_with_neither_email_nor_phone_fails()
    {
        var handler = new AdminGrantCreditsHandler(new FakeWallet(), AdminUsers(), Config());

        var result = await handler.Handle(
            new AdminGrantCreditsCommand(AdminUserId, new AdminGrantCreditsRequest(null, null, 5, "")), default);

        Assert.False(result.Success);
    }
}
