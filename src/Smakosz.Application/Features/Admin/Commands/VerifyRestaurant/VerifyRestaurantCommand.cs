using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.VerifyRestaurant;

public record VerifyRestaurantCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class VerifyRestaurantHandler : IRequestHandler<VerifyRestaurantCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VerifyRestaurantHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(VerifyRestaurantCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.PublicId == request.PublicId, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        restaurant.IsVerified = true;
        restaurant.Status = RestaurantStatus.Active;
        restaurant.VerifiedAt = DateTime.UtcNow;
        restaurant.VerifiedBy = _currentUser.UserId;

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Restaurant,
            EntityId = restaurant.RestaurantId,
            Actor = ModerationActor.Admin,
            Verdict = ModerationVerdict.Approved,
            ProcessedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        if (restaurant.OwnerId.HasValue)
        {
            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == restaurant.OwnerId.Value, cancellationToken);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            _db.Notifications.Add(new Notification
            {
                UserId = restaurant.OwnerId.Value,
                ActorId = _currentUser.UserId,
                Type = NotificationType.System,
                Title = "Restauracja zweryfikowana",
                Message = $"Twoja restauracja \"{restaurant.RestaurantName}\" została zweryfikowana i jest teraz aktywna.",
                SendPush = sendPush,
                PushStatus = pushStatus,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
