using AmharicHelper.Application.DTOs;
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

    /// <summary>Reviewed providers (live + deactivated) for the manage tab.</summary>
    [HttpGet]
    public async Task<IActionResult> Managed()
    {
        var result = await Mediator.Send(new ListManagedProvidersQuery(CurrentUserId));
        return result.Success ? Ok(result.Value) : StatusCode(403, new { error = result.Error });
    }

    /// <summary>Edit a provider's details.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProviderRequest request)
    {
        var result = await Mediator.Send(new UpdateProviderCommand(CurrentUserId, id, request));
        return result.Success ? Ok(new { ok = true }) : StatusCode(403, new { error = result.Error });
    }

    /// <summary>Activate or deactivate a provider (show/hide from users).</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool value)
    {
        var result = await Mediator.Send(new SetProviderActiveCommand(CurrentUserId, id, value));
        return result.Success ? Ok(new { ok = true }) : StatusCode(403, new { error = result.Error });
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
