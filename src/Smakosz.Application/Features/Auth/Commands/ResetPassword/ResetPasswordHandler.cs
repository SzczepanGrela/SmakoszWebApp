using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordHandler : IRequestHandler<ResetPasswordCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordHandler(ISmakoszDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<ErrorOr<Success>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.Auth.InvalidVerificationCode;

        var verificationCode = await _db.VerificationCodes
            .FirstOrDefaultAsync(
                vc => vc.UserId == user.UserId
                    && vc.Type == VerificationCodeType.ResetPassword
                    && vc.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (verificationCode is null || !_passwordHasher.Verify(request.Code, verificationCode.CodeHash))
            return DomainErrors.Auth.InvalidVerificationCode;

        // Remove used code
        _db.VerificationCodes.Remove(verificationCode);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
