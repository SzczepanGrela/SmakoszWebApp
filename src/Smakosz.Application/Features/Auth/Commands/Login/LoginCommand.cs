using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Auth.Dtos;

namespace Smakosz.Application.Features.Auth.Commands.Login;

public record LoginCommand(
    string Email,
    string Password,
    bool RememberMe = false,
    string? TurnstileToken = null
) : IRequest<ErrorOr<AuthResultDto>>;
