using System.Text.Json;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Configuration;
using WebPush;

namespace Smakosz.Infrastructure.Services;

public class WebPushNotificationService : IPushNotificationService
{
    private readonly WebPushClient _client;
    private readonly VapidOptions _vapid;
    private readonly ILogger<WebPushNotificationService> _logger;

    public WebPushNotificationService(VapidOptions vapid, ILogger<WebPushNotificationService> logger)
    {
        _vapid = vapid;
        _logger = logger;
        _client = new WebPushClient();
        _client.SetVapidDetails(_vapid.Subject, _vapid.PublicKey, _vapid.PrivateKey);
    }

    public async Task SendAsync(string endpoint, string p256dh, string auth, string title, string body, string? url = null, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { title, body, url });
        var subscription = new PushSubscription(endpoint, p256dh, auth);

        try
        {
            await _client.SendNotificationAsync(subscription, payload);
            _logger.LogInformation("Push sent to {Endpoint}", endpoint[..Math.Min(60, endpoint.Length)]);
        }
        catch (WebPushException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Gone)
        {
            _logger.LogInformation("Push subscription expired (410 Gone): {Endpoint}", endpoint[..Math.Min(60, endpoint.Length)]);
            throw;
        }
        catch (WebPushException ex)
        {
            _logger.LogWarning(ex, "Push failed ({Status}): {Endpoint}", ex.StatusCode, endpoint[..Math.Min(60, endpoint.Length)]);
            throw;
        }
    }
}
