namespace Smakosz.Application.Common.Interfaces;

public interface IModerationAggregationService
{
    Task AggregateAsync(int textBatchSize, int imageBatchSize, CancellationToken ct);
}
