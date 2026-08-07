using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Wallet;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using Xunit;

namespace AmharicHelper.UnitTests;

public class SponsorshipFeatureTests
{
    // ---- Test doubles (this project has no mocking library — see UploadDocumentHandlerTests) ----

    private sealed class FakeWallet : IWalletService
    {
        public bool AllowDebit { get; set; } = true;
        public int BalanceToReturn { get; set; }
        public List<(UsageSubject Subject, int Credits, string Note)> Debits { get; } = new();
        public List<(UsageSubject Subject, int Credits, string Note)> SponsorshipGrants { get; } = new();

        public Task<int> GetBalanceAsync(UsageSubject subject, CancellationToken ct = default) => Task.FromResult(BalanceToReturn);
        public Task<bool> TryConsumeAsync(UsageSubject subject, string operation, Guid? documentId = null, CancellationToken ct = default) =>
            Task.FromResult(true);
        public Task GrantAsync(UsageSubject subject, int credits, UsageLedgerKind kind, string note, CancellationToken ct = default) =>
            Task.CompletedTask;
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
            Task.FromResult<IReadOnlyList<UsageLedgerEntry>>(Array.Empty<UsageLedgerEntry>());
    }

    private sealed class FakeSponsorships : ISponsorshipRepository
    {
        public Sponsorship? Added { get; private set; }
        public Sponsorship? ToReturnByHash { get; set; }
        public List<(Guid Id, Guid RedeemedBy, DateTime RedeemedAt)> MarkCalls { get; } = new();
        public bool AllowMarkRedeemed { get; set; } = true;
        public IReadOnlyList<Sponsorship> ToReturnList { get; set; } = Array.Empty<Sponsorship>();

        public Task AddAsync(Sponsorship sponsorship, CancellationToken ct = default) { Added = sponsorship; return Task.CompletedTask; }
        public Task<Sponsorship?> GetByRedeemTokenHashAsync(string redeemTokenHash, CancellationToken ct = default) =>
            Task.FromResult(ToReturnByHash);
        public Task<bool> TryMarkRedeemedAsync(Guid id, Guid redeemedByUserId, DateTime redeemedAt, CancellationToken ct = default)
        {
            MarkCalls.Add((id, redeemedByUserId, redeemedAt));
            return Task.FromResult(AllowMarkRedeemed);
        }
        public Task<IReadOnlyList<Sponsorship>> ListBySponsorAsync(Guid sponsorUserId, CancellationToken ct = default) =>
            Task.FromResult(ToReturnList);
    }

    // ---- CreateSponsorshipHandler ----

    [Fact]
    public async Task Create_rejects_non_positive_credits()
    {
        var handler = new CreateSponsorshipHandler(new FakeSponsorships(), new FakeWallet());
        var result = await handler.Handle(
            new CreateSponsorshipCommand(Guid.NewGuid(), new CreateSponsorshipRequest("+972500000000", 0)), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_rejects_empty_phone()
    {
        var handler = new CreateSponsorshipHandler(new FakeSponsorships(), new FakeWallet());
        var result = await handler.Handle(
            new CreateSponsorshipCommand(Guid.NewGuid(), new CreateSponsorshipRequest("", 5)), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Create_fails_and_persists_nothing_when_sponsor_cannot_cover_the_gift()
    {
        var sponsorships = new FakeSponsorships();
        var wallet = new FakeWallet { AllowDebit = false };
        var handler = new CreateSponsorshipHandler(sponsorships, wallet);

        var result = await handler.Handle(
            new CreateSponsorshipCommand(Guid.NewGuid(), new CreateSponsorshipRequest("+972500000000", 5)), default);

        Assert.False(result.Success);
        Assert.Equal(WalletErrors.OutOfCredits, result.Error);
        Assert.Null(sponsorships.Added);
    }

    [Fact]
    public async Task Create_debits_the_sponsor_and_persists_a_hashed_token()
    {
        var sponsorUserId = Guid.NewGuid();
        var sponsorships = new FakeSponsorships();
        var wallet = new FakeWallet { AllowDebit = true };
        var handler = new CreateSponsorshipHandler(sponsorships, wallet);

        var result = await handler.Handle(
            new CreateSponsorshipCommand(sponsorUserId, new CreateSponsorshipRequest("+972500000000", 5)), default);

        Assert.True(result.Success);
        Assert.Equal(5, result.Value!.Credits);
        Assert.NotEmpty(result.Value.RedeemToken);

        Assert.Single(wallet.Debits);
        Assert.Equal(UsageSubject.ForUser(sponsorUserId), wallet.Debits[0].Subject);
        Assert.Equal(5, wallet.Debits[0].Credits);

        Assert.NotNull(sponsorships.Added);
        Assert.Equal(sponsorUserId, sponsorships.Added!.SponsorUserId);
        Assert.Equal(UsageSubject.ForPhone("+972500000000").ContactHash, sponsorships.Added.BeneficiaryContactHash);
        // The persisted hash must never equal the raw token handed back to the caller.
        Assert.NotEqual(result.Value.RedeemToken, sponsorships.Added.RedeemTokenHash);
        Assert.NotEmpty(sponsorships.Added.RedeemTokenHash);
    }

    // ---- RedeemSponsorshipHandler ----

    [Fact]
    public async Task Redeem_with_blank_token_fails()
    {
        var handler = new RedeemSponsorshipHandler(new FakeSponsorships(), new FakeWallet());
        var result = await handler.Handle(new RedeemSponsorshipCommand(Guid.NewGuid(), ""), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Redeem_unknown_token_fails()
    {
        var sponsorships = new FakeSponsorships { ToReturnByHash = null };
        var handler = new RedeemSponsorshipHandler(sponsorships, new FakeWallet());
        var result = await handler.Handle(new RedeemSponsorshipCommand(Guid.NewGuid(), "sometoken"), default);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Redeem_already_redeemed_token_fails_without_granting_anything()
    {
        var sponsorships = new FakeSponsorships
        {
            ToReturnByHash = new Sponsorship
            {
                Credits = 5, RedeemedByUserId = Guid.NewGuid(), RedeemedAt = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAt = DateTime.UtcNow.AddDays(10)
            }
        };
        var wallet = new FakeWallet();
        var handler = new RedeemSponsorshipHandler(sponsorships, wallet);

        var result = await handler.Handle(new RedeemSponsorshipCommand(Guid.NewGuid(), "sometoken"), default);

        Assert.False(result.Success);
        Assert.Empty(wallet.SponsorshipGrants);
        Assert.Empty(sponsorships.MarkCalls);
    }

    [Fact]
    public async Task Redeem_expired_token_fails()
    {
        var sponsorships = new FakeSponsorships
        {
            ToReturnByHash = new Sponsorship { Credits = 5, ExpiresAt = DateTime.UtcNow.AddDays(-1) }
        };
        var handler = new RedeemSponsorshipHandler(sponsorships, new FakeWallet());

        var result = await handler.Handle(new RedeemSponsorshipCommand(Guid.NewGuid(), "sometoken"), default);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Redeem_loses_the_mark_race_fails_without_granting_twice()
    {
        var sponsorships = new FakeSponsorships
        {
            ToReturnByHash = new Sponsorship { Id = Guid.NewGuid(), Credits = 5, ExpiresAt = DateTime.UtcNow.AddDays(10) },
            AllowMarkRedeemed = false // another request won the race and redeemed it first
        };
        var wallet = new FakeWallet();
        var handler = new RedeemSponsorshipHandler(sponsorships, wallet);

        var result = await handler.Handle(new RedeemSponsorshipCommand(Guid.NewGuid(), "sometoken"), default);

        Assert.False(result.Success);
        Assert.Empty(wallet.SponsorshipGrants);
    }

    [Fact]
    public async Task Redeem_success_marks_redeemed_and_grants_credits_to_the_redeemer()
    {
        var sponsorshipId = Guid.NewGuid();
        var redeemerId = Guid.NewGuid();
        var sponsorships = new FakeSponsorships
        {
            ToReturnByHash = new Sponsorship { Id = sponsorshipId, Credits = 7, ExpiresAt = DateTime.UtcNow.AddDays(10) }
        };
        var wallet = new FakeWallet { BalanceToReturn = 7 };
        var handler = new RedeemSponsorshipHandler(sponsorships, wallet);

        var result = await handler.Handle(new RedeemSponsorshipCommand(redeemerId, "sometoken"), default);

        Assert.True(result.Success);
        Assert.Equal(7, result.Value!.CreditsGranted);
        Assert.Equal(7, result.Value.NewBalance);

        Assert.Single(sponsorships.MarkCalls);
        Assert.Equal((sponsorshipId, redeemerId), (sponsorships.MarkCalls[0].Id, sponsorships.MarkCalls[0].RedeemedBy));

        Assert.Single(wallet.SponsorshipGrants);
        Assert.Equal(UsageSubject.ForUser(redeemerId), wallet.SponsorshipGrants[0].Subject);
        Assert.Equal(7, wallet.SponsorshipGrants[0].Credits);
    }

    // ---- ListMySponsorshipsHandler ----

    [Fact]
    public async Task List_maps_redeemed_state_correctly()
    {
        var sponsorships = new FakeSponsorships
        {
            ToReturnList = new[]
            {
                new Sponsorship { Credits = 3, RedeemedByUserId = null },
                new Sponsorship { Credits = 5, RedeemedByUserId = Guid.NewGuid() },
            }
        };
        var handler = new ListMySponsorshipsHandler(sponsorships);

        var result = await handler.Handle(new ListMySponsorshipsQuery(Guid.NewGuid()), default);

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Count);
        Assert.False(result.Value[0].Redeemed);
        Assert.True(result.Value[1].Redeemed);
    }
}
