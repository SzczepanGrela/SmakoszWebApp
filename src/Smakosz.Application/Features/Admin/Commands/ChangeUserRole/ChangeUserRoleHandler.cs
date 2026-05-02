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

namespace Smakosz.Application.Features.Admin.Commands.ChangeUserRole;

public class ChangeUserRoleHandler : IRequestHandler<ChangeUserRoleCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ChangeUserRoleHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ChangeUserRoleCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PublicId == request.PublicId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (user.UserId == _currentUser.UserId)
            return DomainErrors.Admin.CannotChangeOwnRole;

        if (user.Role == request.NewRole)
            return Result.Success;

        if (user.Role == UserRole.Admin && request.NewRole != UserRole.Admin)
        {
            var adminCount = await _db.Users
                .CountAsync(u => u.Role == UserRole.Admin && !u.IsDeleted, cancellationToken);
            if (adminCount <= 1)
                return DomainErrors.Admin.CannotDemoteLastAdmin;
        }

        var oldRole = user.Role;
        var now = DateTime.UtcNow;

        user.Role = request.NewRole;
        user.UpdatedAt = now;

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.RoleChanged,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            Email = user.Email,
            UserId = user.UserId,
            Details = JsonSerializer.Serialize(new
            {
                from = oldRole.ToString(),
                to = request.NewRole.ToString(),
                reason = request.Reason,
                admin_id = _currentUser.UserId
            }),
            CreatedAt = now
        });

        _db.AuditLogs.Add(new AuditLog
        {
            TableName = "users",
            RecordId = user.UserId,
            Operation = AuditOperation.Update,
            ChangedBy = _currentUser.UserId?.ToString() ?? "system",
            ChangedAt = now,
            OldValues = JsonSerializer.Serialize(new { Role = oldRole.ToString() }),
            NewValues = JsonSerializer.Serialize(new { Role = request.NewRole.ToString(), Reason = request.Reason })
        });

        var pushSettings = await _db.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == user.UserId, cancellationToken);
        var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

        var reasonSuffix = string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Powod: {request.Reason}";
        _db.Notifications.Add(new Notification
        {
            UserId = user.UserId,
            ActorId = _currentUser.UserId,
            Type = NotificationType.System,
            Severity = NotificationSeverity.Info,
            Title = "Zmiana roli konta",
            Message = $"Twoja rola zostala zmieniona na {request.NewRole}.{reasonSuffix}",
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
