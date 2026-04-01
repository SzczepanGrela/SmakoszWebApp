using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Commands.ModerateReview;

public record ModerateReviewCommand(Guid PublicId, bool Approve, string? RejectionReason) : IRequest<ErrorOr<Success>>;

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

        review.ModerationStatus = request.Approve ? ContentModerationStatus.Approved : ContentModerationStatus.Rejected;
        review.IsApproved = request.Approve;

        if (!request.Approve)
            review.ContentRejectionReason = request.RejectionReason;

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
                RejectionReason = request.RejectionReason,
                ProcessedAt = now,
                CreatedAt = now
            });
        }
        else
        {
            existing.Status = review.ModerationStatus;
            existing.RejectionReason = request.RejectionReason;
            existing.ProcessedAt = now;
            existing.UpdatedAt = now;
        }

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.Review,
            EntityId = review.ReviewId,
            Actor = ModerationActor.Admin,
            Verdict = request.Approve ? ModerationVerdict.Approved : ModerationVerdict.Rejected,
            ReasonCodes = !string.IsNullOrEmpty(request.RejectionReason) ? [request.RejectionReason] : [],
            ProcessedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        if (!request.Approve)
        {
            _db.Notifications.Add(new Notification
            {
                UserId = review.UserId,
                ActorId = _currentUser.UserId,
                Type = NotificationType.System,
                Severity = NotificationSeverity.Warning,
                Title = "Recenzja odrzucona",
                Message = !string.IsNullOrEmpty(request.RejectionReason)
                    ? $"Twoja recenzja została odrzucona. Powód: {request.RejectionReason}"
                    : "Twoja recenzja została odrzucona przez moderatora.",
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
