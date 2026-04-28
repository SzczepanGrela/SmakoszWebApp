using Amazon.S3;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.HealthChecks;

public class R2HealthCheck : IHealthCheck
{
    private readonly IFileStorageService _storage;

    public R2HealthCheck(IFileStorageService storage)
    {
        _storage = storage;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _storage.CheckConnectivityAsync(cancellationToken);
            return HealthCheckResult.Healthy("R2 reachable");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded("R2 timeout");
        }
        catch (AmazonS3Exception ex)
        {
            return HealthCheckResult.Degraded("R2 error", ex);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("R2 error", ex);
        }
    }
}
