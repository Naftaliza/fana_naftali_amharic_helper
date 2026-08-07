using AmharicHelper.Application.Features.Legal;
using AmharicHelper.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Public Terms/Privacy document lookup, backing /terms and /privacy on the frontend. Anonymous
/// and rate-limited like the other public reads; output-cached since the body only changes when
/// an admin publishes a new version.
/// </summary>
[ApiController]
[Route("api/legal")]
[AllowAnonymous]
[EnableRateLimiting("referrals")]
public class LegalController(IMediator mediator) : ControllerBase
{
    [HttpGet("{kind}")]
    [OutputCache(PolicyName = "legal-doc")]
    public async Task<IActionResult> Get(string kind, [FromQuery] Language language = Language.Hebrew)
    {
        var result = await mediator.Send(new GetLegalDocumentQuery(kind, language));
        return result.Success ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
