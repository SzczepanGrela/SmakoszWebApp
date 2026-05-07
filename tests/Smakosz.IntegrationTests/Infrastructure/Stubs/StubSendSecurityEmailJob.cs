using Smakosz.Application.Common.Interfaces;

namespace Smakosz.IntegrationTests.Infrastructure.Stubs;

public class StubSendSecurityEmailJob : ISendSecurityEmailJob
{
    public Task RunAsync(int notificationId, CancellationToken ct) => Task.CompletedTask;
}
