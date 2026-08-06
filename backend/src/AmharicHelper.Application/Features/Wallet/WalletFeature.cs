using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace AmharicHelper.Application.Features.Wallet;

/// <summary>Monthly free-tier allowance a subject with no other balance is lazily granted the
/// first time they try to spend (see IWalletService.TryConsumeAsync) — kept here rather than in
/// WalletService so the admin console and any future pricing page can display the same number.</summary>
public static class WalletDefaults
{
    public const int FreeTierMonthlyCredits = 3;
}

// ---- Authenticated: my balance + recent history ----
public record GetWalletQuery(Guid UserId) : IRequest<Result<WalletSummaryDto>>;

public class GetWalletHandler(IWalletService wallet) : IRequestHandler<GetWalletQuery, Result<WalletSummaryDto>>
{
    public async Task<Result<WalletSummaryDto>> Handle(GetWalletQuery q, CancellationToken ct)
    {
        var subject = UsageSubject.ForUser(q.UserId);
        var balance = await wallet.GetBalanceAsync(subject, ct);
        var history = await wallet.GetHistoryAsync(subject, 50, ct);

        var historyDtos = history
            .Select(e => new UsageLedgerEntryDto(e.CreatedAt, (int)e.Kind, e.Delta, e.Operation, e.Note, e.DocumentId))
            .ToList();
        return Result<WalletSummaryDto>.Ok(new WalletSummaryDto(
            new WalletBalanceDto(balance, WalletDefaults.FreeTierMonthlyCredits), historyDtos));
    }
}

// ---- Admin: manually grant credits (no payment rail yet — see the plan). Identifies the
// recipient by an existing account's email, or by a raw phone number hashed the same way a
// future WhatsApp identity would be, so a grant made today still lands correctly once that
// contact claims an account later. ----
public record AdminGrantCreditsCommand(Guid AdminUserId, AdminGrantCreditsRequest Request) : IRequest<Result<int>>;

public class AdminGrantCreditsHandler(
    IWalletService wallet,
    IUserRepository users,
    IConfiguration config) : IRequestHandler<AdminGrantCreditsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AdminGrantCreditsCommand cmd, CancellationToken ct)
    {
        var admin = await users.GetByIdAsync(cmd.AdminUserId, ct);
        if (!AdminPolicy.IsAdmin(admin?.Email, config["Admin:Emails"]))
            return Result<int>.Fail("Forbidden");

        var r = cmd.Request;
        if (r.Credits <= 0)
            return Result<int>.Fail("Credits must be a positive number.");

        UsageSubject subject;
        if (!string.IsNullOrWhiteSpace(r.Email))
        {
            var target = await users.GetByEmailAsync(EmailNormalizer.Normalize(r.Email), ct);
            if (target is null)
                return Result<int>.Fail("No account with that email.");
            subject = UsageSubject.ForUser(target.Id);
        }
        else if (!string.IsNullOrWhiteSpace(r.Phone))
        {
            subject = UsageSubject.ForPhone(r.Phone);
        }
        else
        {
            return Result<int>.Fail("Provide either an email or a phone number.");
        }

        await wallet.GrantAsync(subject, r.Credits, UsageLedgerKind.Grant,
            string.IsNullOrWhiteSpace(r.Note) ? "Manual admin grant" : r.Note.Trim(), ct);

        return Result<int>.Ok(await wallet.GetBalanceAsync(subject, ct));
    }
}
