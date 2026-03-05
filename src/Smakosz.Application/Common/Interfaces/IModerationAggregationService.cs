namespace Smakosz.Application.Common.Interfaces;

public interface IModerationAggregationService
{
    Task AggregateAsync(CancellationToken ct);
}
