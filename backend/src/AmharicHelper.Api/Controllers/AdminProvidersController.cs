using AmharicHelper.Application.Features.Partners;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Admin review of pending provider applications. Requires a logged-in user whose email is in
/// the "Admin:Emails" config; non-admins get 403 (the command returns a "Forbidden" result).
/// </summary>
[Authorize]
[Route("api/admin/providers")]
public class AdminProvidersController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpGet("pending")]
    public async Task<IActionResult> Pending()
    {
        var result = await Mediator.Send(new ListPendingProvidersQuery(CurrentUserId));
        return result.Success ? Ok(result.Value) : StatusCode(403, new { error = result.Error });
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var result = await Mediator.Send(new ApproveProviderCommand(CurrentUserId, id));
        return result.Success ? Ok(new { ok = true }) : StatusCode(403, new { error = result.Error });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Reject(Guid id)
    {
        var result = await Mediator.Send(new RejectProviderCommand(CurrentUserId, id));
        return result.Success ? NoContent() : StatusCode(403, new { error = result.Error });
    }
}
