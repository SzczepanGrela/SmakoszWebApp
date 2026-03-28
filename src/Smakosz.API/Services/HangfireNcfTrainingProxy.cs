using ErrorOr;
using Hangfire;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.API.Services;

public class HangfireNcfTrainingProxy : INcfTrainingService
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireNcfTrainingProxy(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public Task<ErrorOr<Success>> ScheduleAsync(CancellationToken ct)
    {
        _jobs.Enqueue<INcfTrainingService>(x => x.ScheduleAsync(CancellationToken.None));
        return Task.FromResult<ErrorOr<Success>>(Result.Success);
    }
}
