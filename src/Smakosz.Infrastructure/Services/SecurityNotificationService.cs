using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Services;

public class SecurityNotificationService : ISecurityNotificationService
{
    private readonly ISmakoszDbContext _db;
    private readonly ISendSecurityEmailJob _emailJob;
    private readonly ILogger<SecurityNotificationService> _logger;

    public SecurityNotificationService(ISmakoszDbContext db, ISendSecurityEmailJob emailJob, ILogger<SecurityNotificationService> logger)
    {
        _db = db;
        _emailJob = emailJob;
        _logger = logger;
    }

    public Task NotifyPasswordChangedAsync(int userId, string? ipAddress, string? countryCode, string? userAgent, CancellationToken ct = default)
    {
        var metadata = new
        {
            subtype = "password_changed",
            ip_address = ipAddress,
            country_code = countryCode,
            user_agent = userAgent,
            occurred_at = DateTime.UtcNow
        };
        return CreateNotificationAsync(
            userId,
            $"security:password_changed:{userId}",
            "Hasło zostało zmienione",
            "Twoje hasło zostało zmienione. Jeśli to nie ty — natychmiast zresetuj hasło.",
            metadata,
            TimeSpan.FromHours(1),
            ct);
    }

    public Task NotifyTwoFactorDisabledAsync(int userId, string? ipAddress, string? countryCode, string? userAgent, CancellationToken ct = default)
    {
        var metadata = new
        {
            subtype = "two_factor_disabled",
            ip_address = ipAddress,
            country_code = countryCode,
            user_agent = userAgent,
            occurred_at = DateTime.UtcNow
        };
        return CreateNotificationAsync(
            userId,
            $"security:two_factor_disabled:{userId}",
            "Dwuskładnikowe uwierzytelnianie zostało wyłączone",
            "2FA zostało wyłączone. Twoje konto jest mniej bezpieczne.",
            metadata,
            TimeSpan.FromHours(1),
            ct);
    }

    public Task NotifyAccountLockedAsync(int userId, int failedAttempts, DateTime lockUntil, string? ipAddress, string? countryCode, string? userAgent, CancellationToken ct = default)
    {
        var metadata = new
        {
            subtype = "account_locked",
            ip_address = ipAddress,
            country_code = countryCode,
            user_agent = userAgent,
            occurred_at = DateTime.UtcNow,
            failed_attempts = failedAttempts,
            lock_until = lockUntil
        };
        return CreateNotificationAsync(
            userId,
            $"security:account_locked:{userId}",
            "Konto tymczasowo zablokowane",
            $"Po {failedAttempts} nieudanych próbach logowania konto zostało zablokowane do {lockUntil:dd.MM.yyyy HH:mm}.",
            metadata,
            TimeSpan.FromHours(1),
            ct);
    }

    public async Task NotifyNewCountryLoginIfApplicableAsync(int userId, string? countryCode, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(countryCode)) return;

        var hasOtherCountryHistory = await _db.SecurityLogs
            .AnyAsync(s => s.UserId == userId
                && s.CountryCode != null
                && s.CountryCode != countryCode
                && s.EventType == SecurityEventType.SuccessfulLogin, ct);
        if (!hasOtherCountryHistory) return;

        var metadata = new
        {
            subtype = "new_country_login",
            ip_address = ipAddress,
            country_code = countryCode,
            user_agent = userAgent,
            occurred_at = DateTime.UtcNow
        };
        await CreateNotificationAsync(
            userId,
            $"security:new_country:{userId}:{countryCode}",
            $"Logowanie z nowego kraju ({countryCode})",
            $"Wykryliśmy logowanie z nowego kraju ({countryCode}). Jeśli to nie ty — sprawdź sesje i zmień hasło.",
            metadata,
            TimeSpan.FromHours(24),
            ct);
    }

    private async Task CreateNotificationAsync(int userId, string groupKey, string title, string message, object metadata, TimeSpan groupWindow, CancellationToken ct)
    {
        var settings = await _db.UserNotificationSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);
        var emailEnabled = settings?.EmailSecurity ?? true;
        var pushEnabled = settings?.PushSecurity ?? false;

        var windowStart = DateTime.UtcNow - groupWindow;
        var existing = await _db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == userId
                && n.GroupKey == groupKey
                && n.CreatedAt >= windowStart
                && !n.IsDeleted, ct);
        if (existing != null)
        {
            existing.Counter++;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            _logger.LogDebug("Security notification for user {UserId} grouped (counter now {Counter}, key {Key})", userId, existing.Counter, groupKey);
            return;
        }

        var notification = new Notification
        {
            UserId = userId,
            Type = NotificationType.Security,
            Title = title,
            Message = message,
            Metadata = JsonSerializer.Serialize(metadata),
            GroupKey = groupKey,
            Severity = NotificationSeverity.Warning,
            SendEmail = emailEnabled,
            SendPush = pushEnabled,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);

        if (emailEnabled)
        {
            await _emailJob.RunAsync(notification.NotificationId, ct);
        }

        _logger.LogInformation("Security notification created for user {UserId} (key {Key}, email={Email})", userId, groupKey, emailEnabled);
    }
}
