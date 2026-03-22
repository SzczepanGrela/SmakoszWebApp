using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.Logout;

public record LogoutCommand(string RefreshToken) : IRequest<ErrorOr<Deleted>>;
