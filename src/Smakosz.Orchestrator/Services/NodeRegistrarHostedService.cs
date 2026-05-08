using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Orchestrator.Configuration;

namespace Smakosz.Orchestrator.Services;

public class NodeRegistrarHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NodesOptions _options;
    private readonly ILogger<NodeRegistrarHostedService> _logger;

    public NodeRegistrarHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<NodesOptions> options,
        ILogger<NodeRegistrarHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();

        await UpsertAsync(db, _options.Api, NodeType.Api, cancellationToken);
        await UpsertAsync(db, _options.RbpiGateway, NodeType.RbpiGateway, cancellationToken);
        await UpsertAsync(db, _options.GpuWorker, NodeType.Gpu, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task UpsertAsync(ISmakoszDbContext db, NodeIdentityConfig config, NodeType nodeType, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(config.NodeId))
        {
            _logger.LogWarning("node-registrar: skipping {NodeType} — empty NodeId in config", nodeType);
            return;
        }

        var existing = await db.SystemNodes.FirstOrDefaultAsync(n => n.NodeId == config.NodeId, ct);

        if (existing is null)
        {
            db.SystemNodes.Add(new SystemNode
            {
                NodeId = config.NodeId,
                NodeType = nodeType,
                Hostname = config.Hostname,
                IpAddress = config.IpAddress,
                WolGatewayId = config.WolGatewayId,
                Status = "offline"
            });
            _logger.LogInformation("node-registrar: inserted {NodeId} (type={NodeType})", config.NodeId, nodeType);
        }
        else
        {
            existing.NodeType = nodeType;
            existing.Hostname = config.Hostname;
            existing.IpAddress = config.IpAddress;
            existing.WolGatewayId = config.WolGatewayId;
            _logger.LogInformation("node-registrar: refreshed identity for {NodeId} (type={NodeType})", config.NodeId, nodeType);
        }
    }
}
