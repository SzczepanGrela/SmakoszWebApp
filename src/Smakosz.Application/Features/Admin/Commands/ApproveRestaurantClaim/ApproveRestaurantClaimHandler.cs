using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ApproveRestaurantClaim;

public class ApproveRestaurantClaimHandler : IRequestHandler<ApproveRestaurantClaimCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ApproveRestaurantClaimHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ApproveRestaurantClaimCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin || !_currentUser.UserId.HasValue)
            return DomainErrors.Admin.Forbidden;

        var ticket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketId == request.TicketId, cancellationToken);

        if (ticket is null)
            return DomainErrors.Ticket.NotFound;

        if (ticket.TicketType != TicketType.RestaurantClaim)
            return DomainErrors.Ticket.WrongType;

        if (ticket.Status != TicketStatus.Open)
            return DomainErrors.Ticket.NotPending;

        if (!ticket.RequesterId.HasValue)
            return DomainErrors.Ticket.RequesterMismatch;

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.RestaurantId == (int)ticket.ReferenceId, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        if (restaurant.OwnerId.HasValue)
            return DomainErrors.Restaurant.AlreadyClaimed;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == ticket.RequesterId.Value && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var alreadyOwns = await _db.Restaurants
            .AnyAsync(r => r.OwnerId == user.UserId, cancellationToken);
        if (alreadyOwns)
            return DomainErrors.Business.UserAlreadyOwnsRestaurant;

        var now = DateTime.UtcNow;
        var oldRole = user.Role;
        var oldStatus = restaurant.Status;

        restaurant.OwnerId = user.UserId;
        restaurant.IsVerified = true;
        if (restaurant.Status != RestaurantStatus.Active)
            restaurant.Status = RestaurantStatus.Active;

        user.Role = UserRole.Restaurant;
        user.UpdatedAt = now;

        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = now;
        ticket.ResolvedByAdminId = _currentUser.UserId.Value;
        ticket.Resolution = $"Claim approved. User {user.Username} now owns restaurant {restaurant.RestaurantName}.";
        ticket.UpdatedAt = now;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "restaurants",
            RecordId = restaurant.RestaurantId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = now,
            OldValues = JsonSerializer.Serialize(new { OwnerId = (int?)null, IsVerified = false, Status = oldStatus.ToString() }),
            NewValues = JsonSerializer.Serialize(new { OwnerId = user.UserId, IsVerified = true, Status = RestaurantStatus.Active.ToString() })
        });

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "users",
            RecordId = user.UserId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = now,
            OldValues = JsonSerializer.Serialize(new { Role = oldRole.ToString() }),
            NewValues = JsonSerializer.Serialize(new { Role = UserRole.Restaurant.ToString(), Reason = $"Approved claim ticket {ticket.TicketId}" })
        });

        var pushSettings = await _db.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == user.UserId, cancellationToken);
        var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

        _db.Notifications.Add(new Notification
        {
            UserId = user.UserId,
            ActorId = _currentUser.UserId,
            Type = NotificationType.System,
            Severity = NotificationSeverity.Success,
            Title = "Claim zaakceptowany",
            Message = $"Twoje zgloszenie przejecia restauracji {restaurant.RestaurantName} zostalo zaakceptowane. Mozesz teraz zarzadzac restauracja w panelu biznesowym.",
            SendEmail = false,
            EmailStatus = EmailStatus.None,
            SendPush = sendPush,
            PushStatus = pushStatus,
            CreatedAt = now
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
