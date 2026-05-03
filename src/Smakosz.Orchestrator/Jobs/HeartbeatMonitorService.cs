using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Configuration;

namespace Smakosz.Orchestrator.Jobs;

public class HeartbeatMonitorService
{
    private readonly ISmakoszDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IDateTimeProvider _clock;
    private readonly GpuWorkerOptions _gpuOptions;
    private readonly ILogger<HeartbeatMonitorService> _logger;

    public HeartbeatMonitorService(
        ISmakoszDbContext db,
        IHttpClientFactory httpFactory,
        IDateTimeProvider clock,
        IOptions<GpuWorkerOptions> gpuOptions,
        ILogger<HeartbeatMonitorService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _clock = clock;
        _gpuOptions = gpuOptions.Value;
        _logger = logger;
    }

    public async Task CheckAsync(CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("GpuWorker");
        string status;

        try
        {
            var response = await client.GetAsync("/health", ct);
            status = response.IsSuccessStatusCode ? "online" : "degraded";
        }
        catch
        {
            status = "offline";
        }

        var node = await _db.SystemNodes
            .FirstOrDefaultAsync(n => n.NodeId == _gpuOptions.NodeId, ct);

        if (node is null)
        {
            _logger.LogWarning("heartbeat-monitor: node {NodeId} not found", _gpuOptions.NodeId);
            return;
        }

        var previousStatus = node.Status;
        node.Status = status;
        node.LastHeartbeat = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (previousStatus != status)
        {
            _logger.LogInformation(
                "heartbeat-monitor: {NodeId} status changed {Old} -> {New}",
                _gpuOptions.NodeId, previousStatus, status);
        }
    }
}
