using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ModeratePhoto;

internal static class ModeratePhotoLogic
{
    public static async Task ApplyAsync(
        MediaAsset asset,
        bool approve,
        string? resolvedRejectionText,
        IReadOnlyList<string> appliedCodes,
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        asset.ModerationStatus = approve ? ContentModerationStatus.Approved : ContentModerationStatus.Rejected;

        if (approve && asset.UploadedBy.HasValue)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == asset.UploadedBy.Value, ct);
            if (user is not null)
                user.PhotoCount++;
        }

        if (!approve)
            asset.RejectionReason = resolvedRejectionText;

        var existingResult = await db.ModerationResults
            .FirstOrDefaultAsync(r => r.EntityType == ModerationEntityType.Photo && r.EntityId == (int)asset.AssetId, ct);
        var now = DateTime.UtcNow;
        if (existingResult is null)
        {
            db.ModerationResults.Add(new ModerationResult
            {
                EntityType = ModerationEntityType.Photo,
                EntityId = (int)asset.AssetId,
                Status = asset.ModerationStatus,
                RejectionReason = resolvedRejectionText,
                ProcessedAt = now,
                CreatedAt = now
            });
        }
        else
        {
            existingResult.Status = asset.ModerationStatus;
            existingResult.RejectionReason = resolvedRejectionText;
            existingResult.ProcessedAt = now;
            existingResult.UpdatedAt = now;
        }

        db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Photo,
            EntityId = (int)asset.AssetId,
            Actor = ModerationActor.Admin,
            Verdict = approve ? ModerationVerdict.Approved : ModerationVerdict.Rejected,
            ReasonCodes = appliedCodes.ToList(),
            ProcessedBy = currentUser.UserId,
            CreatedAt = now
        });

        if (!approve && asset.UploadedBy.HasValue)
        {
            var pushSettings = await db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == asset.UploadedBy.Value, ct);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            db.Notifications.Add(new Notification
            {
                UserId = asset.UploadedBy.Value,
                ActorId = currentUser.UserId,
                Type = NotificationType.System,
                Severity = NotificationSeverity.Warning,
                Title = "Zdjęcie odrzucone",
                Message = $"Twoje zdjęcie zostało odrzucone. Powód: {resolvedRejectionText}",
                SendPush = sendPush,
                PushStatus = pushStatus,
                CreatedAt = now
            });
        }

        var relatedTicket = await db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketType == TicketType.Photo
                && t.ReferenceId == asset.AssetId
                && t.Status != TicketStatus.Resolved
                && t.Status != TicketStatus.Closed, ct);
        if (relatedTicket != null)
        {
            relatedTicket.Status = TicketStatus.Resolved;
            relatedTicket.AssignedAdminId = currentUser.UserId;
        }
    }
}
