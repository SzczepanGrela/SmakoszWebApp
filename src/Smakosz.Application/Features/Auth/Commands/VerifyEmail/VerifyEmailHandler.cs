using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.VerifyEmail;

public class VerifyEmailHandler : IRequestHandler<VerifyEmailCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public VerifyEmailHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var verificationCode = await _db.VerificationCodes
            .FirstOrDefaultAsync(
                vc => vc.UserId == _currentUser.UserId.Value
                    && vc.CodeHash == request.Code
                    && vc.Type == VerificationCodeType.Register
                    && vc.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (verificationCode is null)
            return DomainErrors.Auth.InvalidVerificationCode;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        user.EmailVerified = true;
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
