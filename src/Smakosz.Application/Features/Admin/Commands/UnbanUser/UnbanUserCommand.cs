using ErrorOr;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

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
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
