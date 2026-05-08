using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Orchestrator.Configuration;
using Smakosz.Orchestrator.Services;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Orchestrator.Services;

[Trait("Category", "Handlers")]
public class NodeRegistrarHostedServiceTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IServiceScopeFactory _scopeFactory;

    public NodeRegistrarHostedServiceTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.GetService(typeof(ISmakoszDbContext)).Returns(_db);
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    private NodesOptions DefaultOptions() => new()
    {
        Api = new NodeIdentityConfig { NodeId = "vps-hetzner-prod", Hostname = "hetznerVPS", IpAddress = "100.64.0.5" },
        RbpiGateway = new NodeIdentityConfig { NodeId = "rbpi-gateway", Hostname = "raspberry-pi", IpAddress = "100.64.0.10" },
        GpuWorker = new NodeIdentityConfig { NodeId = "gpu-homelab", Hostname = "homelab", IpAddress = "100.64.0.20", WolGatewayId = "rbpi-gateway" }
    };

    private NodeRegistrarHostedService CreateService(NodesOptions options) =>
        new(_scopeFactory, Options.Create(options), NullLogger<NodeRegistrarHostedService>.Instance);

    [Fact]
    public async Task StartAsync_EmptyDatabase_InsertsThreeRowsWithCorrectTypes()
    {
        var service = CreateService(DefaultOptions());

        await service.StartAsync(CancellationToken.None);

        _sets.SystemNodes.Should().HaveCount(3);
        _sets.SystemNodes.Should().ContainSingle(n => n.NodeId == "vps-hetzner-prod" && n.NodeType == NodeType.Api && n.Hostname == "hetznerVPS");
        _sets.SystemNodes.Should().ContainSingle(n => n.NodeId == "rbpi-gateway" && n.NodeType == NodeType.RbpiGateway && n.IpAddress == "100.64.0.10");
        _sets.SystemNodes.Should().ContainSingle(n => n.NodeId == "gpu-homelab" && n.NodeType == NodeType.Gpu && n.WolGatewayId == "rbpi-gateway");
        _sets.SystemNodes.Should().AllSatisfy(n => n.Status.Should().Be("offline"));
        await _db.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_ExistingRowsWithDynamicState_PreservesStatusAndHeartbeatAndGpuMemory()
    {
        var existingHeartbeat = new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc);
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "gpu-homelab",
            NodeType = NodeType.Gpu,
            Hostname = "old-hostname",
            IpAddress = "10.0.0.99",
            Status = "online",
            LastHeartbeat = existingHeartbeat,
            GpuName = "GTX 1060",
            GpuMemoryTotal = 6144,
            GpuMemoryUsed = 2048,
            CurrentJobId = 42
        });
        DbContextMockFactory.Refresh(_db, _sets);
        var service = CreateService(DefaultOptions());

        await service.StartAsync(CancellationToken.None);

        var gpu = _sets.SystemNodes.Single(n => n.NodeId == "gpu-homelab");
        gpu.Hostname.Should().Be("homelab");
        gpu.IpAddress.Should().Be("100.64.0.20");
        gpu.Status.Should().Be("online");
        gpu.LastHeartbeat.Should().Be(existingHeartbeat);
        gpu.GpuName.Should().Be("GTX 1060");
        gpu.GpuMemoryUsed.Should().Be(2048);
        gpu.CurrentJobId.Should().Be(42);
    }

    [Fact]
    public async Task StartAsync_WrongNodeTypeForRbpi_CorrectsTypeFromConfig()
    {
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "rbpi-gateway",
            NodeType = NodeType.Orchestrator,
            Status = "online"
        });
        DbContextMockFactory.Refresh(_db, _sets);
        var service = CreateService(DefaultOptions());

        await service.StartAsync(CancellationToken.None);

        var rbpi = _sets.SystemNodes.Single(n => n.NodeId == "rbpi-gateway");
        rbpi.NodeType.Should().Be(NodeType.RbpiGateway);
        rbpi.Status.Should().Be("online");
    }
}
