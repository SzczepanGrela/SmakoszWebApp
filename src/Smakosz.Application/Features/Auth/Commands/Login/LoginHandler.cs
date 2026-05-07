using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Helpers;
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
    private readonly IVerificationCodeService _verificationCodeService;
    private readonly IEmailService _emailService;
    private readonly IBusinessMetrics _metrics;

    public LoginHandler(ISmakoszDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService, ISessionService sessionService, ICurrentUserService currentUser, ITurnstileService turnstile, IValidationConfigProvider config, IVerificationCodeService verificationCodeService, IEmailService emailService, IBusinessMetrics metrics)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _sessionService = sessionService;
        _currentUser = currentUser;
        _turnstile = turnstile;
        _config = config;
        _verificationCodeService = verificationCodeService;
        _emailService = emailService;
        _metrics = metrics;
    }

    public async Task<ErrorOr<AuthResultDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        if (!await _turnstile.VerifyAsync(request.TurnstileToken ?? string.Empty, cancellationToken))
        {
            _metrics.RecordLogin("captcha_failed");
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
                CountryCode = _currentUser.CountryCode,
                Email = request.Email.ToLowerInvariant(),
                Details = SecurityLogDetails.BannedIdentifier(),
                CreatedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            _metrics.RecordLogin("account_locked");
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
                CountryCode = _currentUser.CountryCode,
                Email = request.Email.ToLowerInvariant(),
                Details = SecurityLogDetails.AccountLocked(),
                CreatedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);
            _metrics.RecordLogin("account_locked");
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
                CountryCode = _currentUser.CountryCode,
                Email = request.Email.ToLowerInvariant(),
                CreatedAt = now
            });
            await _db.SaveChangesAsync(cancellationToken);
            _metrics.RecordLogin("wrong_password");
            return DomainErrors.Auth.InvalidCredentials;
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;

        if (!user.IsActive)
            return DomainErrors.Auth.AccountInactive;

        if (user.IsBanned)
        {
            _metrics.RecordLogin("account_locked");
            return DomainErrors.Auth.AccountBanned;
        }

        if (!user.EmailVerified)
        {
            _metrics.RecordLogin("email_not_verified");
            return DomainErrors.Auth.EmailNotVerified;
        }

        if (user.Is2faEnabled)
        {
            var twoFactorCode = await _verificationCodeService.CreateCodeAsync(
                user.UserId, VerificationCodeType.TwoFactorAuth, cancellationToken);
            await _emailService.Send2faCodeAsync(user.Email, twoFactorCode, cancellationToken);
            _db.EmailLogs.Add(new EmailLog
            {
                Type = "TwoFactorAuth",
                Recipient = user.Email,
                Subject = "Kod 2FA",
                Status = "sent",
                CreatedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            _metrics.RecordLogin("2fa_required");
            return DomainErrors.Auth.TwoFactorRequired;
        }

        var refreshToken = await _sessionService.CreateSessionAsync(user.UserId, request.RememberMe, cancellationToken);
        var accessTtl = await _sessionService.GetAccessTokenLifetimeSecondsAsync(cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, TimeSpan.FromSeconds(accessTtl));

        user.LastLoginAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        _metrics.RecordLogin("success");

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
