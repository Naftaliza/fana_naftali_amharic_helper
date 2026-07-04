using AmharicHelper.Application.Features.Organizations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Public tenant branding lookup. A white-labeled front end (e.g. netanya.fana.app or
/// ?org=netanya) fetches this by slug on load to theme itself — logo, colors, welcome copy.
/// Anonymous and rate-limited like the other public referral-style endpoints. Output-cached
/// for 2 minutes since branding changes are rare and this fires on every tenant page load.
/// </summary>
[ApiController]
[Route("api/organizations")]
[AllowAnonymous]
[EnableRateLimiting("referrals")]
public class OrganizationsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{slug}")]
    [OutputCache(PolicyName = "org-branding")]
    [ResponseCache(Duration = 120, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var result = await mediator.Send(new GetOrganizationBySlugQuery(slug));
        return result.Success ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
