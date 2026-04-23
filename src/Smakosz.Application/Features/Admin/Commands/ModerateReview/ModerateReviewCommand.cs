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

namespace Smakosz.Application.Features.Admin.Commands.ModerateReview;

public record ModerateReviewCommand(
    Guid PublicId,
    bool Approve,
    IReadOnlyList<string>? ReasonCodes,
    string? ModeratorNote) : IRequest<ErrorOr<Success>>;

public class ModerateReviewValidator : AbstractValidator<ModerateReviewCommand>
{
    public ModerateReviewValidator()
    {
        RuleFor(x => x.ModeratorNote)
            .MaximumLength(500)
            .WithMessage("Uwaga moderatora może mieć maksymalnie 500 znaków");

        RuleFor(x => x)
            .Must(HasAtLeastOneReasonWhenRejecting)
            .WithMessage("Odrzucenie wymaga wybrania co najmniej jednego powodu lub wpisania uwagi moderatora")
            .When(x => !x.Approve);
    }

    private static bool HasAtLeastOneReasonWhenRejecting(ModerateReviewCommand command)
    {
        var hasCodes = command.ReasonCodes is not null
            && command.ReasonCodes.Any(c => !string.IsNullOrWhiteSpace(c));
        var hasNote = !string.IsNullOrWhiteSpace(command.ModeratorNote);
        return hasCodes || hasNote;
    }
}

public class ModerateReviewHandler : IRequestHandler<ModerateReviewCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ModerateReviewHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ModerateReviewCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.PublicId == request.PublicId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return DomainErrors.Review.NotFound;

        string? resolvedText = null;
        IReadOnlyList<string> appliedCodes = Array.Empty<string>();

        if (!request.Approve)
        {
            var resolution = await RejectionReasonResolver.ResolveAsync(
                _db, request.ReasonCodes, request.ModeratorNote, RejectionReasonCategory.Text, cancellationToken);

            if (resolution.IsError)
                return resolution.Errors;

            resolvedText = resolution.Value.ResolvedText;
            appliedCodes = resolution.Value.AppliedCodes;
        }

        review.ModerationStatus = request.Approve ? ContentModerationStatus.Approved : ContentModerationStatus.Rejected;
        review.IsApproved = request.Approve;

        if (!request.Approve)
            review.ContentRejectionReason = resolvedText;

        var existing = await _db.ModerationResults
            .FirstOrDefaultAsync(r => r.EntityType == ModerationEntityType.Review && r.EntityId == review.ReviewId, cancellationToken);
        var now = DateTime.UtcNow;
        if (existing is null)
        {
            _db.ModerationResults.Add(new ModerationResult
            {
                EntityType = ModerationEntityType.Review,
                EntityId = review.ReviewId,
                Status = review.ModerationStatus,
                RejectionReason = resolvedText,
                ProcessedAt = now,
                CreatedAt = now
            });
        }
        else
        {
            existing.Status = review.ModerationStatus;
            existing.RejectionReason = resolvedText;
            existing.ProcessedAt = now;
            existing.UpdatedAt = now;
        }

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Review,
            EntityId = review.ReviewId,
            Actor = ModerationActor.Admin,
            Verdict = request.Approve ? ModerationVerdict.Approved : ModerationVerdict.Rejected,
            ReasonCodes = appliedCodes.ToList(),
            ProcessedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        if (!request.Approve)
        {
            var pushSettings = await _db.UserNotificationSettings
                .FirstOrDefaultAsync(s => s.UserId == review.UserId, cancellationToken);
            var (sendPush, pushStatus) = NotificationPushHelper.Resolve(pushSettings, NotificationType.System);

            _db.Notifications.Add(new Notification
            {
                UserId = review.UserId,
                ActorId = _currentUser.UserId,
                Type = NotificationType.System,
                Severity = NotificationSeverity.Warning,
                Title = "Recenzja odrzucona",
                Message = $"Twoja recenzja została odrzucona. Powód: {resolvedText}",
                SendPush = sendPush,
                PushStatus = pushStatus,
                CreatedAt = DateTime.UtcNow
            });
        }

        var relatedTicket = await _db.SystemTickets
            .FirstOrDefaultAsync(t => t.TicketType == TicketType.ReviewContent
                && t.ReferenceId == review.ReviewId
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
