using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

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

        if (_currentUser.Role is not "User" and not "user")
            return DomainErrors.Social.UserRoleOnly;

        var targetUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Slug == request.Slug && !u.IsDeleted, cancellationToken);

        if (targetUser is null)
            return DomainErrors.User.NotFound;

        if (targetUser.UserId == _currentUser.UserId.Value)
            return DomainErrors.Follow.CannotFollowSelf;

        var currentUser = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value && !u.IsDeleted, cancellationToken);

        if (currentUser is null)
            return DomainErrors.User.NotFound;

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

        targetUser.FollowersCount++;
        currentUser.FollowingCount++;

        var groupKey = $"follow:{targetUser.UserId}";
        var existingNotification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == targetUser.UserId
                && n.GroupKey == groupKey
                && !n.IsRead, cancellationToken);

        var pushSettings = await _db.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == targetUser.UserId, cancellationToken);
        var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.Follow);

        if (existingNotification != null)
        {
            existingNotification.Counter++;
            existingNotification.ActorId = _currentUser.UserId.Value;
            existingNotification.CreatedAt = DateTime.UtcNow;
            existingNotification.Message = $"Ktoś i {existingNotification.Counter - 1} innych zaczęło Cię obserwować.";
            existingNotification.SendPush = sendPush;
            existingNotification.PushStatus = pushStatus;
        }
        else
        {
            _db.Notifications.Add(new Notification
            {
                UserId = targetUser.UserId,
                ActorId = _currentUser.UserId.Value,
                Type = NotificationType.Follow,
                Title = "Nowy obserwujący",
                Message = "Ktoś zaczął Cię obserwować.",
                GroupKey = groupKey,
                Counter = 1,
                SendPush = sendPush,
                PushStatus = pushStatus,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
