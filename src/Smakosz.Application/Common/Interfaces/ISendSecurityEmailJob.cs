namespace Smakosz.Application.Common.Interfaces;

public interface ISendSecurityEmailJob
{
    Task RunAsync(int notificationId, CancellationToken ct);
}
