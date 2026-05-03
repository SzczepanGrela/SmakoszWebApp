using System.Text.Json;
using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.RejectNewRestaurantRequest;

public class RejectNewRestaurantRequestHandler : IRequestHandler<RejectNewRestaurantRequestCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public RejectNewRestaurantRequestHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(RejectNewRestaurantRequestCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin || !_currentUser.UserId.HasValue)
            return DomainErrors.Admin.Forbidden;

        var ticket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketId == request.TicketId, cancellationToken);

        if (ticket is null)
            return DomainErrors.Ticket.NotFound;

        if (ticket.TicketType != TicketType.RestaurantRequest)
            return DomainErrors.Ticket.WrongType;

        if (ticket.Status != TicketStatus.Open)
            return DomainErrors.Ticket.NotPending;

        var now = DateTime.UtcNow;
        ticket.Status = TicketStatus.Rejected;
        ticket.ResolvedAt = now;
        ticket.ResolvedByAdminId = _currentUser.UserId.Value;
        ticket.Resolution = request.Reason;
        ticket.UpdatedAt = now;

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "system_tickets",
            RecordId = ticket.TicketId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = now,
            NewValues = JsonSerializer.Serialize(new { Status = TicketStatus.Rejected.ToString(), Resolution = request.Reason })
        });

        if (ticket.RequesterId.HasValue)
        {
            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == ticket.RequesterId.Value, cancellationToken);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            _db.Notifications.Add(new Notification
            {
                UserId = ticket.RequesterId.Value,
                ActorId = _currentUser.UserId,
                Type = NotificationType.System,
                Severity = NotificationSeverity.Warning,
                Title = "Zgłoszenie nowej restauracji odrzucone",
                Message = $"Twoja prosba o dodanie nowej restauracji zostala odrzucona. Powód: {request.Reason}",
                SendEmail = false,
                EmailStatus = EmailStatus.None,
                SendPush = sendPush,
                PushStatus = pushStatus,
                CreatedAt = now
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
