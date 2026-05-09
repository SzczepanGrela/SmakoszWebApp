using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.DeleteAccount;

public record RequestAccountDeletionCommand(string Password) : IRequest<ErrorOr<Success>>;

public class RequestAccountDeletionHandler : IRequestHandler<RequestAccountDeletionCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly IEmailService _emailService;

    public RequestAccountDeletionHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IVerificationCodeService verificationCodeService,
        IEmailService emailService)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _verificationCodeService = verificationCodeService;
        _emailService = emailService;
    }

    public async Task<ErrorOr<Success>> Handle(RequestAccountDeletionCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (user.Role == UserRole.Admin)
            return DomainErrors.Account.AdminCannotDeleteOwn;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return DomainErrors.Auth.InvalidCredentials;

        var ownsRestaurant = await _db.Restaurants
            .AnyAsync(r => r.OwnerId == userId, cancellationToken);
        if (ownsRestaurant)
            return DomainErrors.Account.IsRestaurantOwner;

        var code = await _verificationCodeService.CreateCodeAsync(
            userId, VerificationCodeType.AccountDeletion, cancellationToken);

        await _emailService.SendAccountDeletionCodeAsync(user.Email, code, cancellationToken);

        return Result.Success;
    }
}
