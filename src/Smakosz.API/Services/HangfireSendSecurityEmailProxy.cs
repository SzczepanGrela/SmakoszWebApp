using Hangfire;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.API.Services;

public class HangfireSendSecurityEmailProxy : ISendSecurityEmailJob
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireSendSecurityEmailProxy(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public Task RunAsync(int notificationId, CancellationToken ct)
    {
        _jobs.Enqueue<ISendSecurityEmailJob>(x => x.RunAsync(notificationId, CancellationToken.None));
        return Task.CompletedTask;
    }
}
