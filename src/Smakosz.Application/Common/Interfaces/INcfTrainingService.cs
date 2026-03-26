using ErrorOr;

namespace Smakosz.Application.Common.Interfaces;

public interface INcfTrainingService
{
    Task<ErrorOr<Success>> ScheduleAsync(CancellationToken ct);
}
