using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.Orchestrator.HealthChecks;

public class HangfireServerHealthCheck : IHealthCheck
{
    private readonly SmakoszDbContext _db;

    public HangfireServerHealthCheck(SmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM hangfire.server WHERE lastheartbeat > NOW() - INTERVAL '1 minute'")
                .FirstOrDefaultAsync(cancellationToken);

            return count > 0
                ? HealthCheckResult.Healthy("Hangfire server active")
                : HealthCheckResult.Unhealthy("No Hangfire server heartbeat");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Hangfire check failed", ex);
        }
    }
}
