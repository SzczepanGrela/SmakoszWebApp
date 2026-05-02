using ErrorOr;
using MediatR;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ChangeUserRole;

public record ChangeUserRoleCommand(
    Guid PublicId,
    UserRole NewRole,
    string? Reason
) : IRequest<ErrorOr<Success>>;
