using ErrorOr;
using MediatR;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.CreatePrivilegedAccount;

public record CreatePrivilegedAccountCommand(
    string Email,
    string Username,
    UserRole Role
) : IRequest<ErrorOr<Guid>>;
