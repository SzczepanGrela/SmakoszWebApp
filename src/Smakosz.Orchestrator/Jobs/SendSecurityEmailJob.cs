using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class SendSecurityEmailJob : ISendSecurityEmailJob
{
    private readonly ISmakoszDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<SendSecurityEmailJob> _logger;

    public SendSecurityEmailJob(ISmakoszDbContext db, IEmailService email, ILogger<SendSecurityEmailJob> logger)
    {
        _db = db;
        _email = email;
        _logger = logger;
    }

    public async Task RunAsync(int notificationId, CancellationToken ct)
    {
        var notif = await _db.Notifications
            .Include(n => n.User)
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId, ct);
        if (notif == null)
        {
            _logger.LogWarning("Security notification {Id} not found, skipping email", notificationId);
            return;
        }
        if (!notif.SendEmail)
        {
            _logger.LogDebug("Security notification {Id} has SendEmail=false, skipping email", notificationId);
            return;
        }
        if (string.IsNullOrEmpty(notif.Metadata))
        {
            _logger.LogWarning("Security notification {Id} has no metadata, skipping email", notificationId);
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(notif.Metadata);
            var root = doc.RootElement;
            var subtype = root.GetProperty("subtype").GetString();
            var ip = root.TryGetProperty("ip_address", out var ipEl) && ipEl.ValueKind != JsonValueKind.Null ? ipEl.GetString() : null;
            var country = root.TryGetProperty("country_code", out var ccEl) && ccEl.ValueKind != JsonValueKind.Null ? ccEl.GetString() : null;
            var occurredAt = root.GetProperty("occurred_at").GetDateTime();

            switch (subtype)
            {
                case "password_changed":
                    await _email.SendSecurityPasswordChangedAsync(notif.User.Email, ip, country, occurredAt, ct);
                    break;
                case "two_factor_disabled":
                    await _email.SendSecurityTwoFactorDisabledAsync(notif.User.Email, ip, country, occurredAt, ct);
                    break;
                case "account_locked":
                    var attempts = root.GetProperty("failed_attempts").GetInt32();
                    var lockUntil = root.GetProperty("lock_until").GetDateTime();
                    await _email.SendSecurityAccountLockedAsync(notif.User.Email, attempts, lockUntil, ip, country, ct);
                    break;
                case "new_country_login":
                    var ua = root.TryGetProperty("user_agent", out var uaEl) && uaEl.ValueKind != JsonValueKind.Null ? uaEl.GetString() : null;
                    await _email.SendSecurityNewCountryLoginAsync(notif.User.Email, country!, ip, ua, occurredAt, ct);
                    break;
                default:
                    _logger.LogWarning("Unknown security subtype {Subtype} on notification {Id}", subtype, notificationId);
                    return;
            }

            notif.EmailStatus = EmailStatus.Sent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send security email for notification {Id}", notificationId);
            notif.EmailStatus = EmailStatus.Failed;
            await _db.SaveChangesAsync(ct);
            throw;
        }

        await _db.SaveChangesAsync(ct);
    }
}
