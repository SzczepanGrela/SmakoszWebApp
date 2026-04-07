using Smakosz.Application.Common.Interfaces;

namespace Smakosz.IntegrationTests.Infrastructure.Stubs;

public class StubModerationAggregationService : IModerationAggregationService
{
    public Task AggregateAsync(int textBatchSize, int imageBatchSize, CancellationToken ct) => Task.CompletedTask;
}
