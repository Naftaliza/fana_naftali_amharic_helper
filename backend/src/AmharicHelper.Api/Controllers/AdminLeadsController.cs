using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Partners;
using AmharicHelper.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

/// <summary>Leads overview for admins (per-provider counts + recent leads) and lifecycle
/// status updates so billing can key off real conversions.</summary>
[Authorize]
[Route("api/admin/leads")]
public class AdminLeadsController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var result = await Mediator.Send(new GetLeadsOverviewQuery(CurrentUserId));
        return result.Success ? Ok(result.Value) : StatusCode(403, new { error = result.Error });
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateLeadStatusRequest body)
    {
        var result = await Mediator.Send(new UpdateLeadStatusCommand(CurrentUserId, id, (LeadStatus)body.Status));
        return result.Success ? Ok(new { ok = true }) : StatusCode(403, new { error = result.Error });
    }
}
