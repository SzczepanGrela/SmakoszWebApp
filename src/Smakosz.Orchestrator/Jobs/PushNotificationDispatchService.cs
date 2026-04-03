using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;
using WebPush;

namespace Smakosz.Orchestrator.Jobs;

public class PushNotificationDispatchService
{
    private readonly ISmakoszDbContext _db;
    private readonly IPushNotificationService _push;
    private readonly ILogger<PushNotificationDispatchService> _logger;

    public PushNotificationDispatchService(
        ISmakoszDbContext db,
        IPushNotificationService push,
        ILogger<PushNotificationDispatchService> logger)
    {
        _db = db;
        _push = push;
        _logger = logger;
    }

    public async Task SendAsync(CancellationToken ct)
    {
        var pending = await _db.Notifications
            .Where(n => n.SendPush && n.PushStatus == PushStatus.Pending && !n.IsDeleted)
            .ToListAsync(ct);

        if (pending.Count == 0)
            return;

        var userIds = pending.Select(n => n.UserId).Distinct().ToList();

        var subscriptions = await _db.PushSubscriptions
            .Where(s => userIds.Contains(s.UserId))
            .ToListAsync(ct);

        if (subscriptions.Count == 0)
        {
            foreach (var n in pending)
                n.PushStatus = PushStatus.Failed;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("push-dispatch: {Count} notifications had no subscriptions", pending.Count);
            return;
        }

        var subsByUser = subscriptions.GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.ToList());
        var sent = 0;
        var expiredEndpoints = new List<int>();

        foreach (var notification in pending)
        {
            if (!subsByUser.TryGetValue(notification.UserId, out var userSubs))
            {
                notification.PushStatus = PushStatus.Failed;
                continue;
            }

            var anySent = false;

            foreach (var sub in userSubs)
            {
                try
                {
                    await _push.SendAsync(sub.Endpoint, sub.P256dh, sub.Auth,
                        notification.Title, notification.Message, null, ct);
                    anySent = true;
                }
                catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    expiredEndpoints.Add(sub.PushSubscriptionId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "push-dispatch: failed for subscription {Id}", sub.PushSubscriptionId);
                }
            }

            notification.PushStatus = anySent ? PushStatus.Sent : PushStatus.Failed;
            if (anySent) sent++;
        }

        foreach (var id in expiredEndpoints.Distinct())
        {
            var sub = subscriptions.FirstOrDefault(s => s.PushSubscriptionId == id);
            if (sub is not null)
                _db.PushSubscriptions.Remove(sub);
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "push-dispatch: sent {Sent}/{Total} notifications, removed {Expired} expired subscriptions",
            sent, pending.Count, expiredEndpoints.Distinct().Count());
    }
}
