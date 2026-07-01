using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Referrals;
using AmharicHelper.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Sponsored referrals. Must work for anonymous (trial) users too, so it can't require auth;
/// rate-limited to deter scraping the directory and spamming the lead log.
/// </summary>
[ApiController]
[Route("api/referrals")]
[AllowAnonymous]
[EnableRateLimiting("referrals")]
public class ReferralsController(IMediator mediator) : ControllerBase
{
    /// <summary>Up to 3 active vetted providers matched to a document category.</summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DocumentCategory category = DocumentCategory.Other)
    {
        var result = await mediator.Send(new GetProvidersQuery(category));
        return Ok(result.Value);
    }

    /// <summary>Records a user→provider contact (the billable lead).</summary>
    [HttpPost("{providerId:guid}/lead")]
    public async Task<IActionResult> LogLead(Guid providerId, [FromBody] LogLeadRequest? body)
    {
        var category = (DocumentCategory)(body?.Category ?? (int)DocumentCategory.Other);
        var urgency = (UrgencyLevel)(body?.Urgency ?? (int)UrgencyLevel.Low);
        var result = await mediator.Send(new LogLeadCommand(providerId, category, urgency, body?.DocumentId, body?.Ref));
        return result.Success ? Ok(new { ok = true }) : NotFound(new { error = result.Error });
    }

    /// <summary>Post-contact "did this help?" signal, keyed by the lead's Ref code shown to the
    /// user. No auth — the trial/anonymous flow logs leads too.</summary>
    [HttpPost("feedback/{refCode}")]
    public async Task<IActionResult> SubmitFeedback(string refCode, [FromBody] LeadFeedbackRequest body)
    {
        var result = await mediator.Send(new SubmitLeadFeedbackCommand(refCode, body.Helpful));
        return result.Success ? Ok(new { ok = true }) : NotFound(new { error = result.Error });
    }
}
