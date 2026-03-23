using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class NotificationDigestService
{
    private readonly ISmakoszDbContext _db;
    private readonly IEmailService _email;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<NotificationDigestService> _logger;

    public NotificationDigestService(
        ISmakoszDbContext db,
        IEmailService email,
        IDateTimeProvider clock,
        ILogger<NotificationDigestService> logger)
    {
        _db = db;
        _email = email;
        _clock = clock;
        _logger = logger;
    }

    public async Task SendAsync(CancellationToken ct)
    {
        var pending = await _db.Notifications
            .Include(n => n.User)
            .Where(n => n.SendEmail && n.EmailStatus == EmailStatus.Pending && !n.IsDeleted)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        var grouped = pending.GroupBy(n => n.UserId);
        var sent = 0;

        foreach (var group in grouped)
        {
            var user = group.First().User;
            var notifications = group.ToList();

            var subject = notifications.Count == 1
                ? notifications[0].Title
                : $"Masz {notifications.Count} nowych powiadomień";

            var items = notifications.Select(n => new NotificationItem(n.Title, n.Message)).ToList();

            try
            {
                await _email.SendNotificationDigestAsync(user.Email, subject, items, ct);

                foreach (var n in notifications)
                    n.EmailStatus = EmailStatus.Sent;

                _db.EmailLogs.Add(new EmailLog
                {
                    Type = "NotificationDigest",
                    Recipient = user.Email,
                    Subject = subject,
                    Status = "sent",
                    CreatedAt = _clock.UtcNow,
                    SentAt = _clock.UtcNow
                });

                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "notification-digest: failed to send to {Email}", user.Email);

                foreach (var n in notifications)
                    n.EmailStatus = EmailStatus.Failed;

                _db.EmailLogs.Add(new EmailLog
                {
                    Type = "NotificationDigest",
                    Recipient = user.Email,
                    Subject = subject,
                    Status = "failed",
                    ErrorMessage = ex.Message,
                    CreatedAt = _clock.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "notification-digest: sent {Sent} digests ({Total} notifications)",
            sent, pending.Count);
    }
}
