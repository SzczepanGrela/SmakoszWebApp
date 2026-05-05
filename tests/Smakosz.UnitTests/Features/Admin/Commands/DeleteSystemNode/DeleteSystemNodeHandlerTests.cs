using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.DeleteSystemNode;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.DeleteSystemNode;

[Trait("Category", "Handlers")]
public class DeleteSystemNodeHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;
    private readonly DeleteSystemNodeHandler _handler;

    public DeleteSystemNodeHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(new DateTime(2026, 5, 6, 12, 0, 0, DateTimeKind.Utc));
        _handler = new DeleteSystemNodeHandler(_db, _currentUser, _clock);
    }

    [Fact]
    public async Task Handle_NotAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new DeleteSystemNodeHandler(_db, nonAdmin, _clock);

        var result = await handler.Handle(
            new DeleteSystemNodeCommand("any-node"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NodeMissing_ReturnsNotFound()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new DeleteSystemNodeCommand("missing"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NodeWithRecentHeartbeat_ReturnsNotStale()
    {
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "gpu-1",
            NodeType = NodeType.Gpu,
            Status = "online",
            LastHeartbeat = _clock.UtcNow.AddDays(-1)
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new DeleteSystemNodeCommand("gpu-1"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_NOT_STALE");
    }

    [Fact]
    public async Task Handle_NodeBeyondThreshold_DeletesAndReturnsSuccess()
    {
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "gpu-old",
            NodeType = NodeType.Gpu,
            Status = "offline",
            LastHeartbeat = _clock.UtcNow.AddDays(-10)
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new DeleteSystemNodeCommand("gpu-old"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemNodes.Should().BeEmpty();
        _sets.AuditLogs.Should().HaveCount(1);
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Delete);
    }

    [Fact]
    public async Task Handle_NodeWithNullHeartbeat_DeletesAndReturnsSuccess()
    {
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "gpu-never-seen",
            NodeType = NodeType.Gpu,
            Status = "unknown",
            LastHeartbeat = null
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new DeleteSystemNodeCommand("gpu-never-seen"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemNodes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CustomThresholdFromConfig_AppliesIt()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "nodes.stale_threshold_days", Value = "3" });
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "gpu-stale",
            NodeType = NodeType.Gpu,
            Status = "offline",
            LastHeartbeat = _clock.UtcNow.AddDays(-5)
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new DeleteSystemNodeCommand("gpu-stale"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemNodes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NodeJustWithinCustomThreshold_ReturnsNotStale()
    {
        _sets.SystemConfigs.Add(new SystemConfig { Key = "nodes.stale_threshold_days", Value = "14" });
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "gpu-fresh",
            NodeType = NodeType.Gpu,
            Status = "online",
            LastHeartbeat = _clock.UtcNow.AddDays(-10)
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new DeleteSystemNodeCommand("gpu-fresh"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_NOT_STALE");
    }
}
