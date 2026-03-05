using MediatR;

namespace Smakosz.Application.Features.Worker.Notifications;

public record NcfTrainingCompletedNotification(string ModelVersion) : INotification;
