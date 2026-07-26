using AmharicHelper.Application.Features.Analytics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

/// <summary>Minimal funnel view for admins — event counts over a trailing window. See the plan:
/// before this the app had no product analytics of any kind.</summary>
[Authorize]
[Route("api/admin/analytics")]
public class AdminAnalyticsController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet("funnel")]
    public async Task<IActionResult> Funnel([FromQuery] int days = 30)
    {
        var result = await Mediator.Send(new GetFunnelQuery(CurrentUserId, days));
        return result.Success ? Ok(result.Value) : StatusCode(403, new { error = result.Error });
    }

    [HttpGet("funnel/{eventName}")]
    public async Task<IActionResult> FunnelEventDetails(string eventName, [FromQuery] int days = 30)
    {
        var result = await Mediator.Send(new GetFunnelEventDetailsQuery(CurrentUserId, eventName, days));
        if (result.Success) return Ok(result.Value);
        return result.Error == "Unknown event name" ? BadRequest(new { error = result.Error }) : StatusCode(403, new { error = result.Error });
    }
}
