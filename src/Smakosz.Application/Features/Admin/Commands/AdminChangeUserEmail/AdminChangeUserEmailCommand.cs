using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Application.Features.Admin.Commands.AdminChangeUserEmail;

public record AdminChangeUserEmailCommand(Guid PublicId, string NewEmail) : IRequest<ErrorOr<Success>>;

public class AdminChangeUserEmailHandler : IRequestHandler<AdminChangeUserEmailCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AdminChangeUserEmailHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(AdminChangeUserEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PublicId == request.PublicId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var normalized = request.NewEmail.Trim().ToLower();
        if (string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase))
            return Result.Success;

        var taken = await _db.Users
            .AnyAsync(u => u.UserId != user.UserId && u.Email == normalized && !u.IsDeleted, cancellationToken);
        if (taken)
            return DomainErrors.Admin.EmailAlreadyExists;

        var oldEmail = user.Email;
        user.Email = normalized;
        user.EmailVerified = false;
        user.UpdatedAt = DateTime.UtcNow;

        _db.UserActionLogs.Add(new UserActionLog
        {
            UserId = user.UserId,
            ActorUserId = _currentUser.UserId,
            ActionType = "email_change",
            OldValue = oldEmail,
            NewValue = normalized,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
