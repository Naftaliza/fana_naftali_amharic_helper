using AmharicHelper.Application.Features.Partners;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

/// <summary>Read-only leads overview for admins (per-provider counts + recent leads).</summary>
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
}
