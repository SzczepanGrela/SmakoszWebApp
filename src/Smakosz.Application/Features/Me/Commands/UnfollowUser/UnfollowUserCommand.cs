using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Me.Commands.UnfollowUser;

public record UnfollowUserCommand(string Slug) : IRequest<ErrorOr<Success>>;

public class UnfollowUserHandler : IRequestHandler<UnfollowUserCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UnfollowUserHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UnfollowUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (_currentUser.Role is not "User" and not "user")
            return DomainErrors.Social.UserRoleOnly;

        var targetUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Slug == request.Slug && !u.IsDeleted, cancellationToken);

        if (targetUser is null)
            return DomainErrors.User.NotFound;

        var currentUser = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value && !u.IsDeleted, cancellationToken);

        if (currentUser is null)
            return DomainErrors.User.NotFound;

        var follow = await _db.UserFollows
            .FirstOrDefaultAsync(
                f => f.FollowerId == _currentUser.UserId.Value && f.FollowedId == targetUser.UserId,
                cancellationToken);

        if (follow is null)
            return DomainErrors.Follow.NotFollowing;

        _db.UserFollows.Remove(follow);

        targetUser.FollowersCount = Math.Max(0, targetUser.FollowersCount - 1);
        currentUser.FollowingCount = Math.Max(0, currentUser.FollowingCount - 1);

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
