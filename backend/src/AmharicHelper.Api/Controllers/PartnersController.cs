using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Partners;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Public business self-registration. Anonymous (no account needed) and rate-limited.
/// Submissions are stored as pending (inactive) providers until an admin approves them.
/// </summary>
[ApiController]
[Route("api/partners")]
[AllowAnonymous]
[EnableRateLimiting("referrals")]
public class PartnersController(IMediator mediator) : ControllerBase
{
    [HttpPost("apply")]
    public async Task<IActionResult> Apply([FromBody] ProviderApplicationRequest request)
    {
        var result = await mediator.Send(new ApplyAsProviderCommand(request));
        return result.Success ? Ok(new { ok = true }) : BadRequest(new { error = result.Error });
    }
}
