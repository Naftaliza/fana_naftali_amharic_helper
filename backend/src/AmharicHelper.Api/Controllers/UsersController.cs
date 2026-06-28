using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

[Authorize]
public class UsersController(IMediator mediator, IUserRepository users, IConfiguration config)
    : ApiControllerBase(mediator)
{
    /// <summary>Returns the authenticated user's profile, including whether they are an admin.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await users.GetByIdAsync(CurrentUserId);
        if (user is null) return NotFound();
        var isAdmin = AdminPolicy.IsAdmin(user.Email, config["Admin:Emails"]);
        return Ok(new UserDto(user.Id, user.Email, user.DisplayName, user.PreferredLanguage, isAdmin));
    }
}
