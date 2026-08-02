using System.Security.Cryptography;
using System.Text;
using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Entities;
using AmharicHelper.Domain.Enums;
using MediatR;

namespace AmharicHelper.Application.Features.Wallet;

/// <summary>How long an unredeemed gift link stays valid before it's effectively abandoned.</summary>
public static class SponsorshipDefaults
{
    public const int ExpiryDays = 30;
}

// ---- Create a gift: debits the sponsor's own balance immediately, returns a one-time link ----
public record CreateSponsorshipCommand(Guid SponsorUserId, CreateSponsorshipRequest Request)
    : IRequest<Result<CreateSponsorshipResponse>>;

public class CreateSponsorshipHandler(ISponsorshipRepository sponsorships, IWalletService wallet)
    : IRequestHandler<CreateSponsorshipCommand, Result<CreateSponsorshipResponse>>
{
    public async Task<Result<CreateSponsorshipResponse>> Handle(CreateSponsorshipCommand cmd, CancellationToken ct)
    {
        var r = cmd.Request;
        if (r.Credits <= 0)
            return Result<CreateSponsorshipResponse>.Fail("Credits must be a positive number.");
        if (string.IsNullOrWhiteSpace(r.BeneficiaryPhone))
            return Result<CreateSponsorshipResponse>.Fail("A phone number is required.");

        var sponsor = UsageSubject.ForUser(cmd.SponsorUserId);

        // Debit first: a sponsor can only ever gift credits they already have. If this fails,
        // nothing is persisted — there is no half-created gift.
        if (!await wallet.TryDebitForSponsorshipAsync(sponsor, r.Credits, "Sponsorship gift sent", ct))
            return Result<CreateSponsorshipResponse>.Fail(WalletErrors.OutOfCredits);

        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var sponsorship = new Sponsorship
        {
            SponsorUserId = cmd.SponsorUserId,
            BeneficiaryContactHash = UsageSubject.ForPhone(r.BeneficiaryPhone).ContactHash!,
            Credits = r.Credits,
            RedeemTokenHash = Sha256Hex(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(SponsorshipDefaults.ExpiryDays)
        };
        await sponsorships.AddAsync(sponsorship, ct);

        return Result<CreateSponsorshipResponse>.Ok(
            new CreateSponsorshipResponse(sponsorship.Id, rawToken, sponsorship.Credits, sponsorship.ExpiresAt));
    }

    // Plain SHA-256, not the PBKDF2 IPasswordHasher used for account passwords — see the
    // Sponsorship entity's doc comment for why that's the right call for a random-token lookup.
    internal static string Sha256Hex(string raw) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
}

// ---- Redeem a gift link. One generic failure message for "not found" / "already redeemed" /
// "expired" — same anti-enumeration convention as ResetPasswordHandler/VerifyEmailHandler, so a
// caller can't distinguish which condition failed. ----
public record RedeemSponsorshipCommand(Guid RedeemerUserId, string RawToken)
    : IRequest<Result<RedeemSponsorshipResponseDto>>;

public class RedeemSponsorshipHandler(ISponsorshipRepository sponsorships, IWalletService wallet)
    : IRequestHandler<RedeemSponsorshipCommand, Result<RedeemSponsorshipResponseDto>>
{
    private const string InvalidLink = "This gift link is invalid, already used, or has expired.";

    public async Task<Result<RedeemSponsorshipResponseDto>> Handle(RedeemSponsorshipCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cmd.RawToken))
            return Result<RedeemSponsorshipResponseDto>.Fail(InvalidLink);

        var hash = CreateSponsorshipHandler.Sha256Hex(cmd.RawToken);
        var sponsorship = await sponsorships.GetByRedeemTokenHashAsync(hash, ct);

        if (sponsorship is null
            || sponsorship.RedeemedByUserId is not null
            || sponsorship.ExpiresAt < DateTime.UtcNow)
        {
            return Result<RedeemSponsorshipResponseDto>.Fail(InvalidLink);
        }

        // The conditional UPDATE inside TryMarkRedeemedAsync is what actually prevents a double
        // redemption race — the checks above are a fast-path, not the safety guarantee.
        var redeemedAt = DateTime.UtcNow;
        if (!await sponsorships.TryMarkRedeemedAsync(sponsorship.Id, cmd.RedeemerUserId, redeemedAt, ct))
            return Result<RedeemSponsorshipResponseDto>.Fail(InvalidLink);

        var beneficiary = UsageSubject.ForUser(cmd.RedeemerUserId);
        await wallet.GrantSponsorshipAsync(beneficiary, sponsorship.Credits, "Sponsorship gift received", ct);
        var newBalance = await wallet.GetBalanceAsync(beneficiary, ct);

        return Result<RedeemSponsorshipResponseDto>.Ok(
            new RedeemSponsorshipResponseDto(sponsorship.Credits, newBalance));
    }
}

// ---- My gift history (the sponsor's own view) ----
public record ListMySponsorshipsQuery(Guid SponsorUserId) : IRequest<Result<IReadOnlyList<SponsorshipSummaryDto>>>;

public class ListMySponsorshipsHandler(ISponsorshipRepository sponsorships)
    : IRequestHandler<ListMySponsorshipsQuery, Result<IReadOnlyList<SponsorshipSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<SponsorshipSummaryDto>>> Handle(ListMySponsorshipsQuery q, CancellationToken ct)
    {
        var mine = await sponsorships.ListBySponsorAsync(q.SponsorUserId, ct);
        var dtos = (IReadOnlyList<SponsorshipSummaryDto>)mine
            .Select(s => new SponsorshipSummaryDto(s.Id, s.Credits, s.RedeemedByUserId is not null, s.CreatedAt, s.ExpiresAt))
            .ToList();
        return Result<IReadOnlyList<SponsorshipSummaryDto>>.Ok(dtos);
    }
}
