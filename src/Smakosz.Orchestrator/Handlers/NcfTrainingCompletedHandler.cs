using Hangfire;
using MediatR;
using Smakosz.Application.Features.Worker.Notifications;
using Smakosz.Orchestrator.Jobs;

namespace Smakosz.Orchestrator.Handlers;

public class NcfTrainingCompletedHandler : INotificationHandler<NcfTrainingCompletedNotification>
{
    private readonly IBackgroundJobClient _jobs;

    public NcfTrainingCompletedHandler(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public Task Handle(NcfTrainingCompletedNotification notification, CancellationToken cancellationToken)
    {
        _jobs.Enqueue<NcfModelActivationService>(
            x => x.ActivateAsync(notification.ModelVersion, CancellationToken.None));
        return Task.CompletedTask;
    }
}
