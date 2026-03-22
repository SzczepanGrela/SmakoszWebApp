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
    private readonly ICodeHasher _codeHasher;

    public VerifyEmailHandler(ISmakoszDbContext db, ICodeHasher codeHasher)
    {
        _db = db;
        _codeHasher = codeHasher;
    }

    public async Task<ErrorOr<Success>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.Auth.InvalidVerificationCode;

        if (user.EmailVerified)
            return Result.Success;

        var verificationCodes = await _db.VerificationCodes
            .Where(vc => vc.UserId == user.UserId
                && vc.Type == VerificationCodeType.Register
                && vc.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        var maxAttempts = await GetMaxAttemptsAsync(cancellationToken);

        var verificationCode = verificationCodes
            .FirstOrDefault(vc => vc.AttemptsCount < maxAttempts && _codeHasher.Verify(request.Code, vc.CodeHash));

        if (verificationCode is null)
        {
            foreach (var vc in verificationCodes)
            {
                vc.AttemptsCount++;
            }
            await _db.SaveChangesAsync(cancellationToken);
            return DomainErrors.Auth.InvalidVerificationCode;
        }

        user.EmailVerified = true;
        _db.VerificationCodes.Remove(verificationCode);
        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }

    private async Task<int> GetMaxAttemptsAsync(CancellationToken ct)
    {
        var config = await _db.SystemConfigs
            .FirstOrDefaultAsync(c => c.Key == "auth.verify_code_max_attempts", ct);
        return config is not null && int.TryParse(config.Value, out var v) && v > 0 ? v : 3;
    }
}
