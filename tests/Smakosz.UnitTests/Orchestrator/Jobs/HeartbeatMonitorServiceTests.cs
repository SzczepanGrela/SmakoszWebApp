using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Orchestrator.Jobs;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Orchestrator.Jobs;

[Trait("Category", "Handlers")]
public class HeartbeatMonitorServiceTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IDateTimeProvider _clock;
    private readonly DateTime _now = new(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);

    public HeartbeatMonitorServiceTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(_now);
    }

    private HeartbeatMonitorService CreateService(
        StubHttpMessageHandler? gpuHandler = null,
        StubHttpMessageHandler? rpiHandler = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("GpuWorker").Returns(_ =>
            new HttpClient(gpuHandler ?? new StubHttpMessageHandler(HttpStatusCode.OK))
            { BaseAddress = new Uri("http://gpu.local") });
        factory.CreateClient("RpiGateway").Returns(_ =>
            new HttpClient(rpiHandler ?? new StubHttpMessageHandler(HttpStatusCode.OK))
            { BaseAddress = new Uri("http://rpi.local") });
        return new HeartbeatMonitorService(_db, factory, _clock, NullLogger<HeartbeatMonitorService>.Instance);
    }

    [Fact]
    public async Task CheckAsync_BothHealthy_MarksBothOnline_UpdatesHeartbeat()
    {
        var gpu = new SystemNode { NodeId = "gpu-worker", NodeType = NodeType.Gpu, Status = "offline" };
        var rpi = new SystemNode { NodeId = "rpi-gateway", NodeType = NodeType.RpiGateway, Status = "offline" };
        _sets.SystemNodes.Add(gpu);
        _sets.SystemNodes.Add(rpi);
        DbContextMockFactory.Refresh(_db, _sets);
        var service = CreateService();

        await service.CheckAsync(CancellationToken.None);

        gpu.Status.Should().Be("online");
        gpu.LastHeartbeat.Should().Be(_now);
        rpi.Status.Should().Be("online");
        rpi.LastHeartbeat.Should().Be(_now);
        await _db.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAsync_RpiHttp500_MarksRpiDegraded()
    {
        var rpi = new SystemNode { NodeId = "rpi-gateway", NodeType = NodeType.RpiGateway, Status = "online" };
        _sets.SystemNodes.Add(rpi);
        DbContextMockFactory.Refresh(_db, _sets);
        var service = CreateService(rpiHandler: new StubHttpMessageHandler(HttpStatusCode.InternalServerError));

        await service.CheckAsync(CancellationToken.None);

        rpi.Status.Should().Be("degraded");
    }

    [Fact]
    public async Task CheckAsync_GpuThrows_MarksGpuOffline()
    {
        var gpu = new SystemNode { NodeId = "gpu-worker", NodeType = NodeType.Gpu, Status = "online" };
        _sets.SystemNodes.Add(gpu);
        DbContextMockFactory.Refresh(_db, _sets);
        var service = CreateService(gpuHandler: StubHttpMessageHandler.Throws(new HttpRequestException("dead")));

        await service.CheckAsync(CancellationToken.None);

        gpu.Status.Should().Be("offline");
    }

    [Fact]
    public async Task CheckAsync_NonExternalNodes_AreIgnored()
    {
        var apiNode = new SystemNode { NodeId = "api-main", NodeType = NodeType.Api, Status = "previous-state" };
        var orch = new SystemNode { NodeId = "orchestrator", NodeType = NodeType.Orchestrator, Status = "previous-state" };
        _sets.SystemNodes.Add(apiNode);
        _sets.SystemNodes.Add(orch);
        DbContextMockFactory.Refresh(_db, _sets);
        var service = CreateService();

        await service.CheckAsync(CancellationToken.None);

        apiNode.Status.Should().Be("previous-state");
        orch.Status.Should().Be("previous-state");
        await _db.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
