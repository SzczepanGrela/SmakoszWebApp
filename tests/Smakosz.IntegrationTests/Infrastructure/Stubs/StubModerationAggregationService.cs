using Smakosz.Application.Common.Interfaces;

namespace Smakosz.IntegrationTests.Infrastructure.Stubs;

public class StubModerationAggregationService : IModerationAggregationService
{
    public Task AggregateAsync(CancellationToken ct) => Task.CompletedTask;
}
