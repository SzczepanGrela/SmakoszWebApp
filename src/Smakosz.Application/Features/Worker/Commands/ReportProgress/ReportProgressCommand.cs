using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Worker.Commands.ReportProgress;

public record ReportProgressCommand(
    int JobId,
    int? Epoch,
    double? Loss,
    double? Accuracy,
    double? LearningRate,
    int? CurrentStep,
    int? TotalSteps,
    string? Message
) : IRequest<ErrorOr<Success>>;
