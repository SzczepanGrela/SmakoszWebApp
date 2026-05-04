using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class HeartbeatMonitorService
{
    private readonly ISmakoszDbContext _db;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<HeartbeatMonitorService> _logger;

    public HeartbeatMonitorService(
        ISmakoszDbContext db,
        IHttpClientFactory httpFactory,
        IDateTimeProvider clock,
        ILogger<HeartbeatMonitorService> logger)
    {
        _db = db;
        _httpFactory = httpFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task CheckAsync(CancellationToken ct)
    {
        var nodes = await _db.SystemNodes
            .Where(n => n.NodeType == NodeType.Gpu || n.NodeType == NodeType.RbpiGateway)
            .ToListAsync(ct);

        foreach (var node in nodes)
        {
            var clientName = node.NodeType switch
            {
                NodeType.Gpu => "GpuWorker",
                NodeType.RbpiGateway => "RbpiGateway",
                _ => null
            };
            if (clientName is null) continue;

            var client = _httpFactory.CreateClient(clientName);
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

            var previousStatus = node.Status;
            node.Status = status;
            node.LastHeartbeat = _clock.UtcNow;

            if (previousStatus != status)
            {
                _logger.LogInformation(
                    "heartbeat-monitor: {NodeId} status changed {Old} -> {New}",
                    node.NodeId, previousStatus, status);
            }
        }

        if (nodes.Count > 0)
            await _db.SaveChangesAsync(ct);
    }
}
