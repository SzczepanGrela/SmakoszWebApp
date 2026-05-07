using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.TwoFactor;

public record Confirm2faCommand(string Code) : IRequest<ErrorOr<Success>>;

public class Confirm2faHandler : IRequestHandler<Confirm2faCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICodeHasher _codeHasher;

    public Confirm2faHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        ICodeHasher codeHasher)
    {
        _db = db;
        _currentUser = currentUser;
        _codeHasher = codeHasher;
    }

    public async Task<ErrorOr<Success>> Handle(Confirm2faCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (user.Is2faEnabled)
            return DomainErrors.Auth.TwoFactorAlreadyEnabled;

        var verificationCode = await _db.VerificationCodes
            .FirstOrDefaultAsync(
                vc => vc.UserId == userId
                    && vc.Type == VerificationCodeType.TwoFactorAuth
                    && vc.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        var maxAttempts = await GetMaxAttemptsAsync(cancellationToken);

        if (verificationCode is null
            || verificationCode.AttemptsCount >= maxAttempts
            || !_codeHasher.Verify(request.Code, verificationCode.CodeHash))
        {
            if (verificationCode is not null)
            {
                verificationCode.AttemptsCount++;
                await _db.SaveChangesAsync(cancellationToken);
            }
            return DomainErrors.Auth.InvalidVerificationCode;
        }

        _db.VerificationCodes.Remove(verificationCode);

        user.Is2faEnabled = true;

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.TwoFactorEnabled,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            CountryCode = _currentUser.CountryCode,
            Email = user.Email,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });

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
