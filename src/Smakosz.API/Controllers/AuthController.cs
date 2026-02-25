using Microsoft.AspNetCore.Authorization;
using Smakosz.Application.Features.Auth.Commands.ForgotPassword;
using Smakosz.Application.Features.Auth.Commands.Login;
using Smakosz.Application.Features.Auth.Commands.Logout;
using Smakosz.Application.Features.Auth.Commands.RefreshToken;
using Smakosz.Application.Features.Auth.Commands.Register;
using Smakosz.Application.Features.Auth.Commands.Resend2fa;
using Smakosz.Application.Features.Auth.Commands.ResendVerification;
using Smakosz.Application.Features.Auth.Commands.ResetPassword;
using Smakosz.Application.Features.Auth.Commands.Verify2fa;
using Smakosz.Application.Features.Auth.Commands.VerifyEmail;

namespace Smakosz.API.Controllers;

[Route("api/auth")]
public class AuthController : ApiController
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return ToCreatedResult(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        return ToActionResult(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand command)
    {
        var result = await _mediator.Send(command);
        return ToActionResult(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [Authorize]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPost("verify-2fa")]
    public async Task<IActionResult> Verify2fa([FromBody] Verify2faCommand command)
    {
        var result = await _mediator.Send(command);
        return ToActionResult(result);
    }

    [HttpPost("resend-2fa")]
    public async Task<IActionResult> Resend2fa([FromBody] Resend2faCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }
}

