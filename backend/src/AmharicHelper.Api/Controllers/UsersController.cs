using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

[Authorize]
public class UsersController(IMediator mediator, IUserRepository users) : ApiControllerBase(mediator)
{
    /// <summary>Returns the authenticated user's profile.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetByIdAsync(CurrentUserId);
        if (user is null) return NotFound();
        return Ok(new UserDto(user.Id, user.Email, user.DisplayName, user.PreferredLanguage));
    }
}
