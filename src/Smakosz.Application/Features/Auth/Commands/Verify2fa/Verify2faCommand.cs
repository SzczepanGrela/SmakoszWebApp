using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Auth.Commands.Verify2fa;

public record Verify2faCommand(string Email, string Code) : IRequest<ErrorOr<Smakosz.Application.Features.Auth.Dtos.AuthResultDto>>;
