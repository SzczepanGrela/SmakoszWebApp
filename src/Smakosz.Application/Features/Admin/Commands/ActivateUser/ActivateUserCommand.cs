using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Application.Features.Admin.Commands.ActivateUser;

public record ActivateUserCommand(Guid PublicId) : IRequest<ErrorOr<Success>>;

public class ActivateUserValidator : AbstractValidator<ActivateUserCommand>
{
    public ActivateUserValidator()
    {
        RuleFor(x => x.PublicId)
            .NotEmpty().WithMessage("Identyfikator użytkownika jest wymagany");
    }
}

public class ActivateUserHandler : IRequestHandler<ActivateUserCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ActivateUserHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.PublicId == request.PublicId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (user.IsActive)
            return Result.Success;

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;

        _db.UserActionLogs.Add(new UserActionLog
        {
            UserId = user.UserId,
            ActorUserId = _currentUser.UserId,
            ActionType = "activate",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success;
    }
}
