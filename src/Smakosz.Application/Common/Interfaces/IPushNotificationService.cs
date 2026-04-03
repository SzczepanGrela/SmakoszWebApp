namespace Smakosz.Application.Common.Interfaces;

public interface IPushNotificationService
{
    Task SendAsync(string endpoint, string p256dh, string auth, string title, string body, string? url = null, CancellationToken ct = default);
}
