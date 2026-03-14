using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetSecurityLogs;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetSecurityLogs;

[Trait("Category", "Handlers")]
public class GetSecurityLogsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetSecurityLogsHandler _handler;

    public GetSecurityLogsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetSecurityLogsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedSecurityLogs()
    {
        _sets.SecurityLogs.Add(new SecurityLog { LogId = 1, EventType = SecurityEventType.FailedLogin, Email = "hacker@test.com", CreatedAt = DateTime.UtcNow });
        _sets.SecurityLogs.Add(new SecurityLog { LogId = 2, EventType = SecurityEventType.PasswordChanged, Email = "jan@test.com", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        _sets.SecurityLogs.Add(new SecurityLog { LogId = 3, EventType = SecurityEventType.BannedRegistration, Email = "banned@test.com", CreatedAt = DateTime.UtcNow.AddHours(-2) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetSecurityLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_WithEventTypeFilter_FiltersResults()
    {
        _sets.SecurityLogs.Add(new SecurityLog { LogId = 1, EventType = SecurityEventType.FailedLogin, Email = "hacker@test.com", CreatedAt = DateTime.UtcNow });
        _sets.SecurityLogs.Add(new SecurityLog { LogId = 2, EventType = SecurityEventType.PasswordChanged, Email = "jan@test.com", CreatedAt = DateTime.UtcNow.AddHours(-1) });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetSecurityLogsQuery(new PaginationParams(1, 20), "FailedLogin"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].EventType.Should().Be("FailedLogin");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetSecurityLogsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetSecurityLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
