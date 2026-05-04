using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Services;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Infrastructure.Services;

[Trait("Category", "Handlers")]
public class RbpiGatewayWakeServiceTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IMemoryCache _cache;
    private readonly IDateTimeProvider _clock;
    private readonly DateTime _now = new(2026, 5, 3, 12, 0, 0, DateTimeKind.Utc);

    public RbpiGatewayWakeServiceTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(_now);
    }

    private RbpiGatewayWakeService CreateService(StubHttpMessageHandler handler)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("RbpiGateway").Returns(new HttpClient(handler) { BaseAddress = new Uri("http://rbpi.local") });
        return new RbpiGatewayWakeService(_db, factory, _cache, _clock, NullLogger<RbpiGatewayWakeService>.Instance);
    }

    [Fact]
    public async Task WakeAsync_NoGpuNode_ReturnsGpuNodeNotFound()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        var result = await service.WakeAsync(CancellationToken.None);

        result.Status.Should().Be(GpuWakeStatus.GpuNodeNotFound);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task WakeAsync_GpuOnline_ReturnsAlreadyOnline_NoHttpCall()
    {
        _sets.SystemNodes.Add(new SystemNode { NodeId = "gpu-worker", NodeType = NodeType.Gpu, Status = "online" });
        DbContextMockFactory.Refresh(_db, _sets);
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        var result = await service.WakeAsync(CancellationToken.None);

        result.Status.Should().Be(GpuWakeStatus.AlreadyOnline);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task WakeAsync_WithinRateLimitWindow_ReturnsRateLimited()
    {
        _sets.SystemNodes.Add(new SystemNode { NodeId = "gpu-worker", NodeType = NodeType.Gpu, Status = "offline" });
        DbContextMockFactory.Refresh(_db, _sets);
        _cache.Set("gpu-wake-throttle", true, TimeSpan.FromSeconds(1));
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        var result = await service.WakeAsync(CancellationToken.None);

        result.Status.Should().Be(GpuWakeStatus.RateLimited);
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task WakeAsync_RpiSuccess_ReturnsSent_UpdatesLastHeartbeat_PostsToWake()
    {
        var node = new SystemNode { NodeId = "gpu-worker", NodeType = NodeType.Gpu, Status = "offline" };
        _sets.SystemNodes.Add(node);
        DbContextMockFactory.Refresh(_db, _sets);
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK);
        var service = CreateService(handler);

        var result = await service.WakeAsync(CancellationToken.None);

        result.Status.Should().Be(GpuWakeStatus.Sent);
        node.LastHeartbeat.Should().Be(_now);
        handler.Requests.Should().ContainSingle(r =>
            r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath == "/wake");
        await _db.Received().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WakeAsync_Rpi500_ReturnsGatewayFailed_NoCacheSet_NoSave()
    {
        _sets.SystemNodes.Add(new SystemNode { NodeId = "gpu-worker", NodeType = NodeType.Gpu, Status = "offline" });
        DbContextMockFactory.Refresh(_db, _sets);
        var handler = new StubHttpMessageHandler(HttpStatusCode.InternalServerError);
        var service = CreateService(handler);

        var result = await service.WakeAsync(CancellationToken.None);

        result.Status.Should().Be(GpuWakeStatus.GatewayFailed);
        result.Message.Should().Contain("500");
        _cache.TryGetValue("gpu-wake-throttle", out _).Should().BeFalse();
    }

    [Fact]
    public async Task WakeAsync_RpiThrows_ReturnsGatewayFailed()
    {
        _sets.SystemNodes.Add(new SystemNode { NodeId = "gpu-worker", NodeType = NodeType.Gpu, Status = "offline" });
        DbContextMockFactory.Refresh(_db, _sets);
        var handler = StubHttpMessageHandler.Throws(new HttpRequestException("connection refused"));
        var service = CreateService(handler);

        var result = await service.WakeAsync(CancellationToken.None);

        result.Status.Should().Be(GpuWakeStatus.GatewayFailed);
        result.Message.Should().Contain("connection refused");
    }
}
