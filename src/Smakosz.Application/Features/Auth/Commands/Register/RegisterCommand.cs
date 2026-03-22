using ErrorOr;
using MediatR;
using Smakosz.Application.Features.Auth.Dtos;

namespace Smakosz.Application.Features.Auth.Commands.Register;

public record RegisterCommand(
    string Username,
    string Email,
    string Password
) : IRequest<ErrorOr<AuthResultDto>>;
