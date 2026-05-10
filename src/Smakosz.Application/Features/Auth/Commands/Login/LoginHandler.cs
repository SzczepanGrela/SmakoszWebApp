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
    private readonly ISecurityNotificationService _securityNotifications;

    public LoginHandler(ISmakoszDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService, ISessionService sessionService, ICurrentUserService currentUser, ITurnstileService turnstile, IValidationConfigProvider config, IVerificationCodeService verificationCodeService, IEmailService emailService, IBusinessMetrics metrics, ISecurityNotificationService securityNotifications)
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
        _securityNotifications = securityNotifications;
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

        if (user is not null && user.LockedUntilUtc > now && !IsPrivileged(user.Role))
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
            var lockoutTriggered = false;
            DateTime lockUntil = default;
            int failedCountAtLockout = 0;

            if (user is not null)
            {
                if (IsPrivileged(user.Role))
                {
                    var threshold = _config.GetInt("auth.priv_ip_ban_threshold", 10);
                    var windowMin = _config.GetInt("auth.priv_ip_ban_window_min", 15);
                    var windowStart = now.AddMinutes(-windowMin);

                    var recentFailed = 1 + await _db.SecurityLogs.CountAsync(s =>
                        s.EventType == SecurityEventType.FailedLogin
                        && s.IpAddress == ipAddress
                        && s.Email == request.Email.ToLowerInvariant()
                        && s.CreatedAt > windowStart, cancellationToken);

                    if (recentFailed >= threshold && ipAddress is not null)
                    {
                        var existingBan = await _db.BannedIdentifiers.AnyAsync(b =>
                            b.Type == BannedIdentifierType.Ip
                            && b.Value == ipAddress
                            && (b.ExpiresAt == null || b.ExpiresAt > now), cancellationToken);

                        if (!existingBan)
                        {
                            var banHours = _config.GetInt("auth.priv_ip_ban_hours", 1);
                            _db.BannedIdentifiers.Add(new BannedIdentifier
                            {
                                Type = BannedIdentifierType.Ip,
                                Value = ipAddress,
                                Reason = $"Auto: {threshold}+ failed attempts on privileged account in {windowMin}min",
                                BannedAt = now,
                                ExpiresAt = now.AddHours(banHours)
                            });
                            _db.SecurityLogs.Add(new SecurityLog
                            {
                                EventType = SecurityEventType.BlockedIp,
                                IpAddress = ipAddress,
                                UserAgent = _currentUser.UserAgent,
                                CountryCode = _currentUser.CountryCode,
                                Email = request.Email.ToLowerInvariant(),
                                Details = SecurityLogDetails.AutoBanPrivilegedBruteForce(threshold, banHours),
                                CreatedAt = now
                            });
                        }
                    }
                }
                else
                {
                    user.FailedLoginCount++;
                    var maxAttempts = _config.GetInt("auth.max_login_attempts", 5);
                    if (user.FailedLoginCount >= maxAttempts)
                    {
                        var lockoutMin = _config.GetInt("auth.lockout_duration_min", 15);
                        lockUntil = now.AddMinutes(lockoutMin);
                        user.LockedUntilUtc = lockUntil;
                        lockoutTriggered = true;
                        failedCountAtLockout = user.FailedLoginCount;
                    }
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
            if (lockoutTriggered && user is not null)
            {
                await _securityNotifications.NotifyAccountLockedAsync(user.UserId, failedCountAtLockout, lockUntil, ipAddress, _currentUser.CountryCode, _currentUser.UserAgent, cancellationToken);
            }
            _metrics.RecordLogin("wrong_password");
            return DomainErrors.Auth.InvalidCredentials;
        }

        user.FailedLoginCount = 0;
        user.LockedUntilUtc = null;

        if (!user.IsActive)
        {
            _metrics.RecordLogin("account_inactive");
            return DomainErrors.Auth.AccountInactive;
        }

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
                user.UserId, VerificationCodeType.TwoFactorAuth,
                request.RememberMe ? "r" : null,
                cancellationToken);
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

        var session = await _sessionService.CreateSessionAsync(user.UserId, request.RememberMe, cancellationToken);
        var accessTtl = await _sessionService.GetAccessTokenLifetimeSecondsAsync(cancellationToken);
        var accessToken = _jwtTokenService.GenerateAccessToken(user, TimeSpan.FromSeconds(accessTtl));

        user.LastLoginAt = now;
        _db.SecurityLogs.Add(new SecurityLog
        {
            EventType = SecurityEventType.SuccessfulLogin,
            IpAddress = ipAddress,
            UserAgent = _currentUser.UserAgent,
            CountryCode = _currentUser.CountryCode,
            Email = request.Email.ToLowerInvariant(),
            UserId = user.UserId,
            Details = SecurityLogDetails.LoginSuccess(),
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);
        await _securityNotifications.NotifyNewCountryLoginIfApplicableAsync(user.UserId, _currentUser.CountryCode, ipAddress, _currentUser.UserAgent, cancellationToken);
        _metrics.RecordLogin("success");

        return new AuthResultDto
        {
            AccessToken = accessToken,
            RefreshToken = session.Token,
            ExpiresAt = DateTime.UtcNow.AddSeconds(accessTtl),
            RefreshTokenExpiresAt = session.ExpiresAt,
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

    private static bool IsPrivileged(UserRole role) => role is UserRole.Admin or UserRole.Moderator;
}
