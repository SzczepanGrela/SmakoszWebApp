using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Me.Commands.FollowUser;

public record FollowUserCommand(string Slug) : IRequest<ErrorOr<Success>>;

public class FollowUserHandler : IRequestHandler<FollowUserCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public FollowUserHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(FollowUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var targetUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Slug == request.Slug && !u.IsDeleted, cancellationToken);

        if (targetUser is null)
            return DomainErrors.User.NotFound;

        if (targetUser.UserId == _currentUser.UserId.Value)
            return DomainErrors.Follow.CannotFollowSelf;

        var alreadyFollowing = await _db.UserFollows.AnyAsync(
            f => f.FollowerId == _currentUser.UserId.Value && f.FollowedId == targetUser.UserId,
            cancellationToken);

        if (alreadyFollowing)
            return DomainErrors.Follow.AlreadyFollowing;

        _db.UserFollows.Add(new UserFollow
        {
            FollowerId = _currentUser.UserId.Value,
            FollowedId = targetUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
