using AmharicHelper.Application.Abstractions;
using AmharicHelper.Application.Common;
using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Users;
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

    /// <summary>Exports everything the account owns — profile, documents, analyses, chat history
    /// (GDPR Art. 15 "right of access").</summary>
    [HttpGet("me/export")]
    public async Task<IActionResult> Export()
    {
        var result = await Mediator.Send(new ExportAccountQuery(CurrentUserId));
        return result.Success ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Permanently deletes the account and everything it owns — documents, analyses,
    /// chat history, cached audio, refresh tokens (GDPR Art. 17 "right to erasure"). Irreversible.</summary>
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount()
    {
        var result = await Mediator.Send(new DeleteAccountCommand(CurrentUserId));
        return result.Success ? NoContent() : NotFound(new { error = result.Error });
    }
}
