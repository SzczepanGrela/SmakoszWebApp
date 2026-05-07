using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Me.Commands.ChangePassword;

public record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest<ErrorOr<Success>>;

public class ChangePasswordHandler : IRequestHandler<ChangePasswordCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISecurityNotificationService _securityNotifications;

    public ChangePasswordHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IPasswordHasher passwordHasher, ISecurityNotificationService securityNotifications)
    {
        _db = db;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _securityNotifications = securityNotifications;
    }

    public async Task<ErrorOr<Success>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == _currentUser.UserId.Value && !u.IsDeleted, cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return DomainErrors.Auth.InvalidCredentials;

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.SecurityStamp = Guid.NewGuid().ToString();

        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.PasswordChanged,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            CountryCode = _currentUser.CountryCode,
            Email = user.Email,
            UserId = user.UserId,
            Details = SecurityLogDetails.PasswordChanged(),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        await _securityNotifications.NotifyPasswordChangedAsync(user.UserId, _currentUser.IpAddress, _currentUser.CountryCode, _currentUser.UserAgent, cancellationToken);

        return Result.Success;
    }
}
