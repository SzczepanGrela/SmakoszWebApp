using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.AddSystemNode;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.AddSystemNode;

[Trait("Category", "Handlers")]
public class AddSystemNodeHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly AddSystemNodeHandler _handler;

    public AddSystemNodeHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new AddSystemNodeHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_NotAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new AddSystemNodeHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new AddSystemNodeCommand("test-gpu", "gpu", "AA:BB:CC:DD:EE:FF", null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_ValidGpuNode_AddsAndReturnsSuccess()
    {
        _sets.SystemNodes.Add(new SystemNode
        {
            NodeId = "rbpi-gateway",
            NodeType = NodeType.RbpiGateway,
            Status = "online"
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AddSystemNodeCommand("test-gpu", "gpu", "AA:BB:CC:DD:EE:FF", "rbpi-gateway"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemNodes.Should().HaveCount(2);
        _sets.AuditLogs.Should().HaveCount(1);
        _sets.AuditLogs[0].Operation.Should().Be(AuditOperation.Insert);
    }

    [Fact]
    public async Task Handle_DuplicateNodeId_ReturnsConflict()
    {
        _sets.SystemNodes.Add(new SystemNode { NodeId = "test-gpu", NodeType = NodeType.Gpu, Status = "unknown" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AddSystemNodeCommand("test-gpu", "gpu", "AA:BB:CC:DD:EE:FF", null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Handle_GpuMissingMac_ReturnsValidation()
    {
        _sets.SystemNodes.Add(new SystemNode { NodeId = "rbpi-gateway", NodeType = NodeType.RbpiGateway, Status = "online" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AddSystemNodeCommand("foo-gpu", "gpu", null, "rbpi-gateway"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_GPU_REQUIRES_MAC_AND_GATEWAY");
    }

    [Fact]
    public async Task Handle_GpuMissingGateway_ReturnsValidation()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AddSystemNodeCommand("foo-gpu", "gpu", "AA:BB:CC:DD:EE:FF", null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_GPU_REQUIRES_MAC_AND_GATEWAY");
    }

    [Fact]
    public async Task Handle_GpuWithMissingGatewayNode_ReturnsInvalidGatewayReference()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AddSystemNodeCommand("foo-gpu", "gpu", "AA:BB:CC:DD:EE:FF", "nonexistent-gateway"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_INVALID_GATEWAY");
    }

    [Fact]
    public async Task Handle_GpuWithApiAsGateway_ReturnsInvalidGatewayReference()
    {
        _sets.SystemNodes.Add(new SystemNode { NodeId = "api-node", NodeType = NodeType.Api, Status = "online" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AddSystemNodeCommand("foo-gpu", "gpu", "AA:BB:CC:DD:EE:FF", "api-node"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_INVALID_GATEWAY");
    }

    [Fact]
    public async Task Handle_OrchestratorNode_NoMacRequired_AddsSuccessfully()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AddSystemNodeCommand("orch-2", "orchestrator", null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemNodes.Should().HaveCount(1);
        _sets.SystemNodes[0].NodeId.Should().Be("orch-2");
    }

    [Fact]
    public async Task Handle_InvalidNodeTypeString_ReturnsValidation()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AddSystemNodeCommand("foo", "bogus-type", null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NODE_INVALID_TYPE");
    }
}
