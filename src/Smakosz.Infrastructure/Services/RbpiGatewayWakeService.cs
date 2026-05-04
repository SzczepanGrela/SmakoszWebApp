using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Services;

public class RbpiGatewayWakeService : IGpuWakeService
{
    private const string ThrottleCacheKey = "gpu-wake-throttle";
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromSeconds(1);

    private readonly ISmakoszDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<RbpiGatewayWakeService> _logger;

    public RbpiGatewayWakeService(
        ISmakoszDbContext db,
        IHttpClientFactory httpFactory,
        IMemoryCache cache,
        IDateTimeProvider clock,
        ILogger<RbpiGatewayWakeService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _cache = cache;
        _clock = clock;
        _logger = logger;
    }

    public async Task<GpuWakeResult> WakeAsync(CancellationToken ct)
    {
        var gpuNode = await _db.SystemNodes
            .FirstOrDefaultAsync(n => n.NodeType == NodeType.Gpu, ct);

        if (gpuNode is null)
            return new GpuWakeResult(GpuWakeStatus.GpuNodeNotFound, "No GPU node registered");

        if (gpuNode.Status == "online")
            return new GpuWakeResult(GpuWakeStatus.AlreadyOnline);

        if (_cache.TryGetValue(ThrottleCacheKey, out _))
            return new GpuWakeResult(GpuWakeStatus.RateLimited);

        var client = _httpFactory.CreateClient("RbpiGateway");
        try
        {
            var response = await client.PostAsync("/wake", null, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "gpu-wake: RBPI gateway responded with {StatusCode}",
                    response.StatusCode);
                return new GpuWakeResult(
                    GpuWakeStatus.GatewayFailed,
                    $"Gateway returned {(int)response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "gpu-wake: failed to reach RBPI gateway");
            return new GpuWakeResult(GpuWakeStatus.GatewayFailed, ex.Message);
        }

        _cache.Set(ThrottleCacheKey, true, ThrottleWindow);
        gpuNode.LastHeartbeat = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("gpu-wake: magic packet sent for {NodeId}", gpuNode.NodeId);
        return new GpuWakeResult(GpuWakeStatus.Sent);
    }
}
