using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ChangeRestaurantStatus;

public record ChangeRestaurantStatusCommand(
    Guid PublicId,
    RestaurantStatus NewStatus,
    string? Reason) : IRequest<ErrorOr<Success>>;

public class ChangeRestaurantStatusHandler : IRequestHandler<ChangeRestaurantStatusCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ChangeRestaurantStatusHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ChangeRestaurantStatusCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.PublicId == request.PublicId, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        if (restaurant.Status == request.NewStatus)
            return DomainErrors.Restaurant.SameStatus;

        if (!IsLegalTransition(restaurant.Status, request.NewStatus))
            return DomainErrors.Restaurant.InvalidStatusTransition;

        var oldStatus = restaurant.Status;
        restaurant.Status = request.NewStatus;

        var verdict = request.NewStatus switch
        {
            RestaurantStatus.Active => ModerationVerdict.Approved,
            RestaurantStatus.Suspended => ModerationVerdict.Rejected,
            RestaurantStatus.ClosedPermanently => ModerationVerdict.Rejected,
            _ => ModerationVerdict.NeedsReview
        };

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Restaurant,
            EntityId = restaurant.RestaurantId,
            Actor = ModerationActor.Admin,
            Verdict = verdict,
            AdminNote = request.Reason,
            ProcessedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        _db.AuditLogs.Add(AuditLogHelper.BuildEntry(
            "restaurants",
            restaurant.RestaurantId,
            AuditOperation.Update,
            _currentUser.UserId?.ToString(),
            new { Status = oldStatus.ToString() },
            new { Status = request.NewStatus.ToString(), Reason = request.Reason }));

        if (restaurant.OwnerId.HasValue)
        {
            var message = request.NewStatus switch
            {
                RestaurantStatus.Active => $"Restauracja \"{restaurant.RestaurantName}\" jest ponownie aktywna.",
                RestaurantStatus.Suspended => $"Restauracja \"{restaurant.RestaurantName}\" została zawieszona. Powód: {request.Reason}",
                RestaurantStatus.ClosedPermanently => $"Restauracja \"{restaurant.RestaurantName}\" została trwale zamknięta. Powód: {request.Reason}",
                RestaurantStatus.Renovation => $"Restauracja \"{restaurant.RestaurantName}\" oznaczona jako w remoncie.",
                _ => $"Status restauracji \"{restaurant.RestaurantName}\" został zmieniony."
            };

            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == restaurant.OwnerId.Value, cancellationToken);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            _db.Notifications.Add(new Notification
            {
                UserId = restaurant.OwnerId.Value,
                ActorId = _currentUser.UserId,
                Type = NotificationType.System,
                Title = "Zmiana statusu restauracji",
                Message = message,
                SendPush = sendPush,
                PushStatus = pushStatus,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }

    private static bool IsLegalTransition(RestaurantStatus current, RestaurantStatus next)
    {
        return current switch
        {
            RestaurantStatus.PendingVerification => next is RestaurantStatus.Active
                or RestaurantStatus.Suspended or RestaurantStatus.ClosedPermanently,
            RestaurantStatus.Active => next is RestaurantStatus.Renovation
                or RestaurantStatus.Suspended or RestaurantStatus.ClosedPermanently,
            RestaurantStatus.Renovation => next is RestaurantStatus.Active
                or RestaurantStatus.ClosedPermanently,
            RestaurantStatus.Suspended => next is RestaurantStatus.Active
                or RestaurantStatus.ClosedPermanently,
            RestaurantStatus.ClosedPermanently => false,
            _ => false
        };
    }
}
