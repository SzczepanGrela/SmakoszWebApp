using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ModeratePhoto;

public record ModeratePhotoCommand(
    Guid PublicId,
    bool Approve,
    IReadOnlyList<string>? ReasonCodes,
    string? ModeratorNote) : IRequest<ErrorOr<Success>>;

public class ModeratePhotoValidator : AbstractValidator<ModeratePhotoCommand>
{
    public ModeratePhotoValidator()
    {
        RuleFor(x => x.ModeratorNote)
            .MaximumLength(500)
            .WithMessage("Uwaga moderatora może mieć maksymalnie 500 znaków");

        RuleFor(x => x)
            .Must(HasAtLeastOneReasonWhenRejecting)
            .WithMessage("Odrzucenie wymaga wybrania co najmniej jednego powodu lub wpisania uwagi moderatora")
            .When(x => !x.Approve);
    }

    private static bool HasAtLeastOneReasonWhenRejecting(ModeratePhotoCommand command)
    {
        var hasCodes = command.ReasonCodes is not null
            && command.ReasonCodes.Any(c => !string.IsNullOrWhiteSpace(c));
        var hasNote = !string.IsNullOrWhiteSpace(command.ModeratorNote);
        return hasCodes || hasNote;
    }
}

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

        string? resolvedText = null;
        IReadOnlyList<string> appliedCodes = Array.Empty<string>();

        if (!request.Approve)
        {
            var resolution = await RejectionReasonResolver.ResolveAsync(
                _db, request.ReasonCodes, request.ModeratorNote, RejectionReasonCategory.Photo, cancellationToken);

            if (resolution.IsError)
                return resolution.Errors;

            resolvedText = resolution.Value.ResolvedText;
            appliedCodes = resolution.Value.AppliedCodes;
        }

        asset.ModerationStatus = request.Approve ? ContentModerationStatus.Approved : ContentModerationStatus.Rejected;

        if (request.Approve && asset.UploadedBy.HasValue)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == asset.UploadedBy.Value, cancellationToken);
            if (user is not null)
                user.PhotoCount++;
        }

        if (!request.Approve)
            asset.RejectionReason = resolvedText;

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
                RejectionReason = resolvedText,
                ProcessedAt = now,
                CreatedAt = now
            });
        }
        else
        {
            existingResult.Status = asset.ModerationStatus;
            existingResult.RejectionReason = resolvedText;
            existingResult.ProcessedAt = now;
            existingResult.UpdatedAt = now;
        }

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Photo,
            EntityId = (int)asset.AssetId,
            Actor = ModerationActor.Admin,
            Verdict = request.Approve ? ModerationVerdict.Approved : ModerationVerdict.Rejected,
            ReasonCodes = appliedCodes.ToList(),
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
                Message = $"Twoje zdjęcie zostało odrzucone. Powód: {resolvedText}",
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
