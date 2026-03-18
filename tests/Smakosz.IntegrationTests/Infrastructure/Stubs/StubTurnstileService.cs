using Smakosz.Application.Common.Interfaces;

namespace Smakosz.IntegrationTests.Infrastructure.Stubs;

public class StubTurnstileService : ITurnstileService
{
    public Task<bool> VerifyAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
