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

namespace Smakosz.Application.Features.Admin.Commands.UnbanUser;

public record UnbanUserCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class UnbanUserValidator : AbstractValidator<UnbanUserCommand>
{
    public UnbanUserValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Identyfikator użytkownika jest wymagany");
    }
}

public class UnbanUserHandler : IRequestHandler<UnbanUserCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UnbanUserHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(UnbanUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PublicId == request.PublicId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        user.IsBanned = false;

        var bannedEmail = await _db.BannedIdentifiers
            .FirstOrDefaultAsync(b => b.Type == BannedIdentifierType.Email && b.Value == user.Email, cancellationToken);

        if (bannedEmail is not null)
            _db.BannedIdentifiers.Remove(bannedEmail);

        _db.ModerationLogs.Add(new ModerationLog
        {
            EntityType = ModerationEntityType.User,
            EntityId = user.UserId,
            Actor = ModerationActor.Admin,
            Verdict = ModerationVerdict.Unbanned,
            ProcessedBy = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow
        });

        _db.UserActionLogs.Add(new UserActionLog
        {
            UserId = user.UserId,
            ActorUserId = _currentUser.UserId,
            ActionType = "unban",
            CreatedAt = DateTime.UtcNow
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
            Title = "Konto odblokowane",
            Message = "Twoje konto zostało odblokowane.",
            SendEmail = true,
            EmailStatus = EmailStatus.Pending,
            SendPush = sendPush,
            PushStatus = pushStatus,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
