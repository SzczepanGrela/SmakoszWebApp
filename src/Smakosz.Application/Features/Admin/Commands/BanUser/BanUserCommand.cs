using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.BanUser;

public record BanUserCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class BanUserHandler : IRequestHandler<BanUserCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public BanUserHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(BanUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PublicId == request.PublicId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (_currentUser.UserId.HasValue && user.UserId == _currentUser.UserId.Value)
            return DomainErrors.Admin.CannotBanSelf;

        user.IsBanned = true;

        var alreadyBanned = await _db.BannedIdentifiers.AnyAsync(
            b => b.Type == BannedIdentifierType.Email && b.Value == user.Email, cancellationToken);

        if (!alreadyBanned)
        {
            _db.BannedIdentifiers.Add(new BannedIdentifier
            {
                Type = BannedIdentifierType.Email,
                Value = user.Email,
                Reason = $"Auto-ban: user {user.Username} (ID: {user.UserId}) banned by admin",
                BannedBy = _currentUser.UserId,
                BannedAt = DateTime.UtcNow
            });
        }

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.User,
            EntityId = user.UserId,
            Actor = ModerationActor.Admin,
            Verdict = ModerationVerdict.Banned,
            ProcessedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        _db.UserActionLogs.Add(new UserActionLog
        {
            UserId = user.UserId,
            ActorUserId = _currentUser.UserId,
            ActionType = "ban",
            CreatedAt = DateTime.UtcNow
        });

        var pushSettings = await _db.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == user.UserId, cancellationToken);
        var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

        _db.Notifications.Add(new Notification
        {
            UserId = user.UserId,
            ActorId = _currentUser.UserId,
            Type = NotificationType.System,
            Severity = NotificationSeverity.Danger,
            Title = "Konto zablokowane",
            Message = "Twoje konto zostało zablokowane przez administratora.",
            SendEmail = true,
            EmailStatus = EmailStatus.Pending,
            SendPush = sendPush,
            PushStatus = pushStatus,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
