using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.Register;

public record RegisterCommand(
    string Username,
    string Email,
    string Password
) : IRequest<ErrorOr<Success>>;
