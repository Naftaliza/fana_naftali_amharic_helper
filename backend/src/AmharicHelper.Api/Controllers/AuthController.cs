using AmharicHelper.Application.DTOs;
using AmharicHelper.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AmharicHelper.Api.Controllers;

public class AuthController(IMediator mediator) : ApiControllerBase(mediator)
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var result = await Mediator.Send(new RegisterCommand(request));
        return result.Success ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await Mediator.Send(new LoginCommand(request));
        return result.Success ? Ok(result.Value) : Unauthorized(new { error = result.Error });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request)
    {
        var result = await Mediator.Send(new RefreshCommand(request));
        return result.Success ? Ok(result.Value) : Unauthorized(new { error = result.Error });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await Mediator.Send(new ForgotPasswordCommand(request));
        return Ok(new { message = "If the email exists, reset instructions have been sent." });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        var result = await Mediator.Send(new ResetPasswordCommand(request));
        return result.Success ? Ok(new { message = "Password updated." }) : BadRequest(new { error = result.Error });
    }
}
