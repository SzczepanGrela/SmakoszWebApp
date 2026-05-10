using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Worker.Commands.SendHeartbeat;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Microsoft.Extensions.Logging;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Worker.Commands.SendHeartbeat;

[Trait("Category", "Handlers")]
public class SendHeartbeatHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IDateTimeProvider _clock;
    private readonly SendHeartbeatHandler _handler;
    private static readonly DateTime Now = new(2026, 5, 14, 12, 0, 0, DateTimeKind.Utc);

    public SendHeartbeatHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(Now);
        var logger = Substitute.For<ILogger<SendHeartbeatHandler>>();
        _handler = new SendHeartbeatHandler(_db, _clock, logger);
    }

    [Fact]
    public async Task Handle_KnownNode_UpdatesHeartbeatAndStatus()
    {
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "gpu-homelab",
            NodeType = NodeType.Gpu,
            Role = NodeRole.Worker,
            Status = "offline",
            LastHeartbeat = Now.AddHours(-1)
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new SendHeartbeatCommand("gpu-homelab", "100.64.0.20", "GTX 1060", 6144, 1024, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var node = _sets.SystemNodes.Single();
        node.Status.Should().Be("online");
        node.LastHeartbeat.Should().Be(Now);
        node.IpAddress.Should().Be("100.64.0.20");
        node.GpuMemoryUsed.Should().Be(1024);
    }

    [Fact]
    public async Task Handle_UnknownNodeId_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(
            new SendHeartbeatCommand("rogue-node", null, null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Should().Be(DomainErrors.Node.NotFound);
        _sets.SystemNodes.Should().BeEmpty();
    }
}
