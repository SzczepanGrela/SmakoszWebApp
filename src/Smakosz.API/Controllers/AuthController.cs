using ErrorOr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Smakosz.API.Auth;
using Smakosz.Application.Features.Auth.Commands.AcceptInvite;
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
using Smakosz.Application.Features.Auth.Dtos;

namespace Smakosz.API.Controllers;

[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ApiController
{
    private readonly IMediator _mediator;
    private readonly AuthCookieWriter _cookies;

    public AuthController(IMediator mediator, AuthCookieWriter cookies)
    {
        _mediator = mediator;
        _cookies = cookies;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        return ToSessionResult(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies[CookieNames.Refresh];
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(new ApiResponse<object>
            {
                Success = false,
                Error = new ApiError { Code = "REFRESH_TOKEN_MISSING", Message = "Brak ciasteczka odswiezania." }
            });

        var result = await _mediator.Send(new RefreshTokenCommand(refreshToken));
        return ToSessionResult(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = Request.Cookies[CookieNames.Refresh];
        _cookies.Clear(Response);
        if (string.IsNullOrWhiteSpace(refreshToken))
            return NoContent();

        var result = await _mediator.Send(new LogoutCommand(refreshToken));
        return ToNoContentResult(result);
    }

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
        return ToSessionResult(result);
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

    [HttpPost("accept-invite")]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteCommand command)
    {
        var result = await _mediator.Send(command);
        return ToNoContentResult(result);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var sub = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var expClaim = User.FindFirst("exp")?.Value;
        DateTime? expiresAt = null;
        if (long.TryParse(expClaim, out var expUnix))
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Data = new
            {
                userId = sub,
                username = name,
                email,
                role,
                expiresAt
            }
        });
    }

    private IActionResult ToSessionResult(ErrorOr<AuthResultDto> result)
    {
        if (result.IsError)
            return ToActionResult(result);

        _cookies.Write(
            Response,
            result.Value.AccessToken, result.Value.ExpiresAt,
            result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);

        return Ok(new ApiResponse<AuthResultDto>
        {
            Success = true,
            Data = new AuthResultDto
            {
                AccessToken = string.Empty,
                RefreshToken = string.Empty,
                ExpiresAt = result.Value.ExpiresAt,
                RefreshTokenExpiresAt = result.Value.RefreshTokenExpiresAt,
                User = result.Value.User
            }
        });
    }
}
