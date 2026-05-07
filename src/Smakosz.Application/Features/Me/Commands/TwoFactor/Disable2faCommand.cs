using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.TwoFactor;

public record Disable2faCommand(string Password) : IRequest<ErrorOr<Success>>;

public class Disable2faHandler : IRequestHandler<Disable2faCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecurityNotificationService _securityNotifications;

    public Disable2faHandler(
        ISmakoszDbContext db,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        ISecurityNotificationService securityNotifications)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _securityNotifications = securityNotifications;
    }

    public async Task<ErrorOr<Success>> Handle(Disable2faCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            return DomainErrors.Auth.InvalidCredentials;

        if (!user.Is2faEnabled)
            return DomainErrors.Auth.TwoFactorNotEnabled;

        user.Is2faEnabled = false;

        var pendingCodes = await _db.VerificationCodes
            .Where(vc => vc.UserId == userId && vc.Type == VerificationCodeType.TwoFactorAuth)
            .ToListAsync(cancellationToken);
        _db.VerificationCodes.RemoveRange(pendingCodes);

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.TwoFactorDisabled,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            CountryCode = _currentUser.CountryCode,
            Email = user.Email,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _securityNotifications.NotifyTwoFactorDisabledAsync(userId, _currentUser.IpAddress, _currentUser.CountryCode, _currentUser.UserAgent, cancellationToken);

        return Result.Success;
    }
}
