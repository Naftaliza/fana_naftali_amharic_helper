using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Wallet;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

/// <summary>My credit balance, usage history, and sponsorship (gift-credits) actions.</summary>
[Authorize]
public class WalletController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await Mediator.Send(new GetWalletQuery(CurrentUserId));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Gift credits to someone else. Debits the caller's own balance immediately and
    /// returns a one-time redeem link (the raw token is never stored — see Sponsorship).</summary>
    [HttpPost("sponsor")]
    public async Task<IActionResult> Sponsor(CreateSponsorshipRequest request)
    {
        var result = await Mediator.Send(new CreateSponsorshipCommand(CurrentUserId, request));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Redeem a gift link, crediting the caller's own account.</summary>
    [HttpPost("redeem/{token}")]
    public async Task<IActionResult> Redeem(string token)
    {
        var result = await Mediator.Send(new RedeemSponsorshipCommand(CurrentUserId, token));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>My own gift history — which links I've sent and whether each has been redeemed.</summary>
    [HttpGet("sponsorships")]
    public async Task<IActionResult> Sponsorships()
    {
        var result = await Mediator.Send(new ListMySponsorshipsQuery(CurrentUserId));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }
}

/// <summary>
/// Admin-only: manually grant credits. There is no payment rail yet (see the plan) — this is how
/// the first customers get credited: sell out-of-band (Bit-to-phone, bank transfer) and grant here.
/// </summary>
[Authorize]
[Route("api/admin/wallet")]
public class AdminWalletController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpPost("grant")]
    public async Task<IActionResult> Grant(AdminGrantCreditsRequest request)
    {
        var result = await Mediator.Send(new AdminGrantCreditsCommand(CurrentUserId, request));
        return result.Success ? Ok(new { balance = result.Value }) : StatusCode(403, new { error = result.Error });
    }
}
