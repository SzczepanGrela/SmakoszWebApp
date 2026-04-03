using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ModeratePhoto;

public record ModeratePhotoCommand(Guid PublicId, bool Approve, string? RejectionReason) : IRequest<ErrorOr<Success>>;

public class ModeratePhotoHandler : IRequestHandler<ModeratePhotoCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ModeratePhotoHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ModeratePhotoCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var asset = await _db.MediaAssets
            .FirstOrDefaultAsync(a => a.PublicId == request.PublicId, cancellationToken);

        if (asset is null)
            return DomainErrors.Photo.NotFound;

        asset.ModerationStatus = request.Approve ? ContentModerationStatus.Approved : ContentModerationStatus.Rejected;

        if (request.Approve && asset.UploadedBy.HasValue)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == asset.UploadedBy.Value, cancellationToken);
            if (user is not null)
                user.PhotoCount++;
        }

        if (!request.Approve)
            asset.RejectionReason = request.RejectionReason;

        var existingResult = await _db.ModerationResults
            .FirstOrDefaultAsync(r => r.EntityType == ModerationEntityType.Photo && r.EntityId == (int)asset.AssetId, cancellationToken);
        var now = DateTime.UtcNow;
        if (existingResult is null)
        {
            _db.ModerationResults.Add(new ModerationResult
            {
                EntityType = ModerationEntityType.Photo,
                EntityId = (int)asset.AssetId,
                Status = asset.ModerationStatus,
                RejectionReason = request.RejectionReason,
                ProcessedAt = now,
                CreatedAt = now
            });
        }
        else
        {
            existingResult.Status = asset.ModerationStatus;
            existingResult.RejectionReason = request.RejectionReason;
            existingResult.ProcessedAt = now;
            existingResult.UpdatedAt = now;
        }

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Photo,
            EntityId = (int)asset.AssetId,
            Actor = ModerationActor.Admin,
            Verdict = request.Approve ? ModerationVerdict.Approved : ModerationVerdict.Rejected,
            ReasonCodes = !string.IsNullOrEmpty(request.RejectionReason) ? [request.RejectionReason] : [],
            ProcessedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        if (!request.Approve && asset.UploadedBy.HasValue)
        {
            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == asset.UploadedBy.Value, cancellationToken);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            _db.Notifications.Add(new Notification
            {
                UserId = asset.UploadedBy.Value,
                ActorId = _currentUser.UserId,
                Type = NotificationType.System,
                Severity = NotificationSeverity.Warning,
                Title = "Zdjęcie odrzucone",
                Message = !string.IsNullOrEmpty(request.RejectionReason)
                    ? $"Twoje zdjęcie zostało odrzucone. Powód: {request.RejectionReason}"
                    : "Twoje zdjęcie zostało odrzucone przez moderatora.",
                SendPush = sendPush,
                PushStatus = pushStatus,
                CreatedAt = DateTime.UtcNow
            });
        }

        var relatedTicket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketType == TicketType.Photo
                && t.ReferenceId == asset.AssetId
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
