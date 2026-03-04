using ErrorOr;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.IntegrationTests.Infrastructure.Stubs;

public class StubNcfTrainingService : INcfTrainingService
{
    public Task<ErrorOr<Success>> ScheduleAsync(CancellationToken ct)
        => Task.FromResult<ErrorOr<Success>>(Result.Success);
}
