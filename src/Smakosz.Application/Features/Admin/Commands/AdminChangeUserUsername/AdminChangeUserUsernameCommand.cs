using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Application.Features.Admin.Commands.AdminChangeUserUsername;

public record AdminChangeUserUsernameCommand(Guid PublicId, string NewUsername) : IRequest<ErrorOr<Success>>;

public class AdminChangeUserUsernameHandler : IRequestHandler<AdminChangeUserUsernameCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AdminChangeUserUsernameHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(AdminChangeUserUsernameCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PublicId == request.PublicId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (string.Equals(user.Username, request.NewUsername, StringComparison.Ordinal))
            return Result.Success;

        var taken = await _db.Users
            .AnyAsync(u => u.UserId != user.UserId && u.Username == request.NewUsername && !u.IsDeleted, cancellationToken);
        if (taken)
            return DomainErrors.Admin.UsernameAlreadyExists;

        var oldUsername = user.Username;
        user.Username = request.NewUsername;
        user.UpdatedAt = DateTime.UtcNow;

        _db.UserActionLogs.Add(new UserActionLog
        {
            UserId = user.UserId,
            ActorUserId = _currentUser.UserId,
            ActionType = "username_change",
            OldValue = oldUsername,
            NewValue = request.NewUsername,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
