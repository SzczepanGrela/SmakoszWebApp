using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Auth.Dtos;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Auth.Commands.Login;

public class LoginHandler : IRequestHandler<LoginCommand, ErrorOr<AuthResultDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ISessionService _sessionService;
    private readonly ICurrentUserService _currentUser;
    private readonly ITurnstileService _turnstile;
    private readonly IValidationConfigProvider _config;

    public LoginHandler(ISmakoszDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService, ISessionService sessionService, ICurrentUserService currentUser, ITurnstileService turnstile, IValidationConfigProvider config)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _sessionService = sessionService;
        _currentUser = currentUser;
        _turnstile = turnstile;
        _config = config;
    }

    public async Task<ErrorOr<AuthResultDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (!await _turnstile.VerifyAsync(request.TurnstileToken ?? string.Empty, cancellationToken))
        {
            return DomainErrors.Captcha.VerificationFailed;
        }

        var ipAddress = _currentUser.IpAddress;
        var now = DateTime.UtcNow;

        var isBanned = await _db.BannedIdentifiers.AnyAsync(b =>
            (b.ExpiresAt == null || b.ExpiresAt > now) &&
            (
                (b.Type == BannedIdentifierType.Email && b.Value == request.Email.ToLowerInvariant()) ||
                (b.Type == BannedIdentifierType.Ip && ipAddress != null && b.Value == ipAddress)
            ), cancellationToken);

        if (isBanned)
        {
            _db.SecurityLogs.Add(new SecurityLog
            {
                EventType = SecurityEventType.BlockedIp,
                IpAddress = ipAddress,
                UserAgent = _currentUser.UserAgent,
                Email = request.Email.ToLowerInvariant(),
                Details = "{\"reason\": \"banned_identifier\"}",
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);

            return DomainErrors.Auth.AccountBanned;
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && !u.IsDeleted, cancellationToken);

        if (user is not null && user.LockedUntilUtc > now)
        {
            _db.SecurityLogs.Add(new SecurityLog
            {
                EventType = SecurityEventType.FailedLogin,
                IpAddress = ipAddress,
                UserAgent = _currentUser.UserAgent,
                Email = request.Email.ToLowerInvariant(),
                Details = "{\"reason\": \"account_locked\"}",
                CreatedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);
            return DomainErrors.Auth.AccountLocked;
        }

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            if (user is not null)
            {
                user.FailedLoginCount++;
                var maxAttempts = _config.GetInt("auth.max_login_attempts", 5);
                if (user.FailedLoginCount >= maxAttempts)
                {
                    var lockoutMin = _config.GetInt("auth.lockout_duration_min", 15);
                    user.LockedUntilUtc = now.AddMinutes(lockoutMin);
                }
            }

            _db.SecurityLogs.Add(new SecurityLog
            {
                EventType = SecurityEventType.FailedLogin,
                IpAddress = ipAddress,
                UserAgent = _currentUser.UserAgent,
                Email = request.Email.ToLowerInvariant(),
                CreatedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);

            return DomainErrors.Auth.InvalidCredentials;
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;

        if (!user.IsActive)
            return DomainErrors.Auth.AccountInactive;

        if (user.IsBanned)
            return DomainErrors.Auth.AccountBanned;

        if (!user.EmailVerified)
            return DomainErrors.Auth.EmailNotVerified;

        var refreshToken = await _sessionService.CreateSessionAsync(user.UserId, request.RememberMe, cancellationToken);
        var accessTtl = await _sessionService.GetAccessTokenLifetimeSecondsAsync(cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, TimeSpan.FromSeconds(accessTtl));

        user.LastLoginAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(accessTtl),
            User = new UserProfileDto
            {
                PublicId = user.PublicId,
                Slug = user.Slug ?? string.Empty,
                Username = user.Username,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role.ToString(),
                EmailVerified = user.EmailVerified
            }
        };
    }
}
