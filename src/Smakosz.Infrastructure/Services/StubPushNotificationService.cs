using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class StubPushNotificationService : IPushNotificationService
{
    private readonly ILogger<StubPushNotificationService> _logger;

    public StubPushNotificationService(ILogger<StubPushNotificationService> logger) => _logger = logger;

    public Task SendAsync(string endpoint, string p256dh, string auth, string title, string body, string? url = null, CancellationToken ct = default)
    {
        _logger.LogInformation("[Push Stub] {Title}: {Body} -> {Endpoint}", title, body, endpoint[..Math.Min(60, endpoint.Length)]);
        return Task.CompletedTask;
    }
}
