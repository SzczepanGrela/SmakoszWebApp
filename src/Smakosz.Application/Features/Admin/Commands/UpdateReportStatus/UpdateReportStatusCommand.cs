using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.UpdateReportStatus;

public record UpdateReportStatusCommand(int ReportId, string Status) : IRequest<ErrorOr<Success>>;

public class UpdateReportStatusHandler : IRequestHandler<UpdateReportStatusCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;

    public UpdateReportStatusHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IDateTimeProvider dateTime)
    {
        _db = db;
        _currentUser = currentUser;
        _dateTime = dateTime;
    }

    public async Task<ErrorOr<Success>> Handle(UpdateReportStatusCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var report = await _db.Reports
            .FirstOrDefaultAsync(r => r.ReportId == request.ReportId, cancellationToken);

        if (report is null)
            return DomainErrors.Report.NotFound;

        if (!Enum.TryParse<Smakosz.Domain.Enums.ReportStatus>(request.Status, true, out var statusEnum))
            return DomainErrors.Report.InvalidStatus;

        report.Status = statusEnum;
        report.ResolvedAt = _dateTime.UtcNow;
        report.ResolvedByAdminId = _currentUser.UserId!.Value;

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Report,
            EntityId = report.ReportId,
            Actor = ModerationActor.Admin,
            Verdict = ModerationVerdict.Resolved,
            ReasonCodes = [request.Status],
            ProcessedBy = _currentUser.UserId,
            CreatedAt = _dateTime.UtcNow
        });

        var pushSettings = await _db.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == report.ReporterId, cancellationToken);
        var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

        _db.Notifications.Add(new Notification
        {
            UserId = report.ReporterId,
            ActorId = _currentUser.UserId,
            Type = NotificationType.System,
            Title = "Zgłoszenie rozpatrzone",
            Message = $"Twoje zgłoszenie zostało rozpatrzone ze statusem: {request.Status}.",
            SendPush = sendPush,
            PushStatus = pushStatus,
            CreatedAt = _dateTime.UtcNow
        });

        var relatedTicket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketType == TicketType.Report
                && t.ReferenceId == report.ReportId
                && t.Status != TicketStatus.Resolved
                && t.Status != TicketStatus.Closed, cancellationToken);
        if (relatedTicket != null)
        {
            relatedTicket.Status = TicketStatus.Resolved;
            relatedTicket.AssignedAdminId = _currentUser.UserId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
