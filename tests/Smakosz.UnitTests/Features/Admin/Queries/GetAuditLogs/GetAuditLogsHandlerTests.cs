using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetAuditLogs;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetAuditLogs;

[Trait("Category", "Handlers")]
public class GetAuditLogsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetAuditLogsHandler _handler;

    public GetAuditLogsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetAuditLogsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedAuditLogs()
    {
        _sets.AuditLogs.Add(new AuditLog { AuditLogId = 1, TableName = "config", RecordId = 1, Operation = AuditOperation.Insert, ChangedBy = "admin (4)", ChangedAt = DateTime.UtcNow });
        _sets.AuditLogs.Add(new AuditLog { AuditLogId = 2, TableName = "cities", RecordId = 1, Operation = AuditOperation.Update, ChangedBy = "admin (4)", ChangedAt = DateTime.UtcNow.AddHours(-1) });
        _sets.AuditLogs.Add(new AuditLog { AuditLogId = 3, TableName = "ingredients", RecordId = 1, Operation = AuditOperation.Delete, ChangedBy = "admin (4)", ChangedAt = DateTime.UtcNow.AddHours(-2) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAuditLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithTableNameFilter_FiltersResults()
    {
        _sets.AuditLogs.Add(new AuditLog { AuditLogId = 1, TableName = "config", RecordId = 1, Operation = AuditOperation.Insert, ChangedBy = "admin (4)", ChangedAt = DateTime.UtcNow });
        _sets.AuditLogs.Add(new AuditLog { AuditLogId = 2, TableName = "cities", RecordId = 1, Operation = AuditOperation.Update, ChangedBy = "admin (4)", ChangedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAuditLogsQuery(new PaginationParams(1, 20), "config"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].TableName.Should().Be("config");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetAuditLogsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetAuditLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmptyPage()
    {
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAuditLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().BeEmpty();
        result.Value.Pagination.TotalCount.Should().Be(0);
    }
}
