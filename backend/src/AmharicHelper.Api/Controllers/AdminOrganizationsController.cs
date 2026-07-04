using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Organizations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

/// <summary>
/// Admin management of B2B/B2G tenants: provisioning a new one (sales-assisted for now —
/// no self-serve signup yet) and the usage dashboard that justifies renewal. Requires a
/// logged-in user whose email is in the "Admin:Emails" config, same gate as the provider
/// review console; non-admins get 403.
/// </summary>
[Authorize]
[Route("api/admin/organizations")]
public class AdminOrganizationsController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request)
    {
        var result = await Mediator.Send(new CreateOrganizationCommand(CurrentUserId, request));
        return result.Success ? Ok(new { id = result.Value }) : StatusCode(403, new { error = result.Error });
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var result = await Mediator.Send(new ListOrganizationsQuery(CurrentUserId));
        return result.Success ? Ok(result.Value) : StatusCode(403, new { error = result.Error });
    }

    /// <summary>Edit a tenant's branding (name/logo/colors/welcome text). Slug is immutable.</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrganizationRequest request)
    {
        var result = await Mediator.Send(new UpdateOrganizationCommand(CurrentUserId, id, request));
        return result.Success ? Ok(new { ok = true }) : StatusCode(403, new { error = result.Error });
    }

    /// <summary>Activate or deactivate a tenant. Deactivating hides it from the public branding
    /// lookup (GET /api/organizations/{slug}) for new sessions — existing cached responses may
    /// serve stale branding for up to the 2-minute org-branding cache TTL.</summary>
    [HttpPost("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromQuery] bool value)
    {
        var result = await Mediator.Send(new SetOrganizationActiveCommand(CurrentUserId, id, value));
        return result.Success ? Ok(new { ok = true }) : StatusCode(403, new { error = result.Error });
    }

    /// <summary>Aggregate usage dashboard for one tenant.</summary>
    [HttpGet("{id:guid}/stats")]
    public async Task<IActionResult> Stats(Guid id)
    {
        var result = await Mediator.Send(new GetOrganizationStatsQuery(CurrentUserId, id));
        return result.Success ? Ok(result.Value) : StatusCode(403, new { error = result.Error });
    }
}
