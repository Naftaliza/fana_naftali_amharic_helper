using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase(IMediator mediator) : ControllerBase
{
    protected IMediator Mediator { get; } = mediator;

    /// <summary>The authenticated user's id, parsed from the JWT 'sub' claim.</summary>
    protected Guid CurrentUserId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub"), out var id)
            ? id
            : throw new UnauthorizedAccessException("Missing or invalid user id claim.");
}
