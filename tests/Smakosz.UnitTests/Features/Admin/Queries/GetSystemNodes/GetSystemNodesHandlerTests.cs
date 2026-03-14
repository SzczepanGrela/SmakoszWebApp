using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetSystemNodes;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetSystemNodes;

[Trait("Category", "Handlers")]
public class GetSystemNodesHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetSystemNodesHandler _handler;

    public GetSystemNodesHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetSystemNodesHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsAllNodes()
    {
        _sets.SystemNodes.Add(new SystemNode { NodeId = "api-main", NodeType = NodeType.Api, Role = NodeRole.Dispatcher, Status = "online", Hostname = "vps-1" });
        _sets.SystemNodes.Add(new SystemNode { NodeId = "gpu-worker-1", NodeType = NodeType.Gpu, Role = NodeRole.Worker, Status = "offline", GpuName = "RTX 3060" });
        _sets.SystemNodes.Add(new SystemNode { NodeId = "orchestrator", NodeType = NodeType.Orchestrator, Role = NodeRole.Dispatcher, Status = "online" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetSystemNodesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetSystemNodesHandler(_db, nonAdmin);

        var result = await handler.Handle(new GetSystemNodesQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmptyList()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetSystemNodesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
