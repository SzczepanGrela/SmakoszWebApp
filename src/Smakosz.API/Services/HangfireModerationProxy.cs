using Hangfire;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.API.Services;

public class HangfireModerationProxy : IModerationAggregationService
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireModerationProxy(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public Task AggregateAsync(int textBatchSize, int imageBatchSize, CancellationToken ct)
    {
        _jobs.Enqueue<IModerationAggregationService>(x => x.AggregateAsync(textBatchSize, imageBatchSize, CancellationToken.None));
        return Task.CompletedTask;
    }
}
