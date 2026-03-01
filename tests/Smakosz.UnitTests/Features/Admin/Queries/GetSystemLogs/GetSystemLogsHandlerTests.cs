using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetSystemLogs;
using Smakosz.Domain.Entities.System;
using Smakosz.UnitTests.Common.TestInfrastructure;
using DomainLogLevel = Smakosz.Domain.Enums.LogLevel;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetSystemLogs;

[Trait("Category", "Handlers")]
public class GetSystemLogsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetSystemLogsHandler _handler;

    public GetSystemLogsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetSystemLogsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedLogs()
    {
        _sets.SystemLogs.Add(new SystemLog { Id = 1, Source = "API", Level = DomainLogLevel.Info, Message = "Started", CreatedAt = DateTime.UtcNow });
        _sets.SystemLogs.Add(new SystemLog { Id = 2, Source = "API", Level = DomainLogLevel.Error, Message = "Failed", CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetSystemLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithLevelFilter_FiltersResults()
    {
        _sets.SystemLogs.Add(new SystemLog { Id = 1, Source = "API", Level = DomainLogLevel.Info, Message = "Started", CreatedAt = DateTime.UtcNow });
        _sets.SystemLogs.Add(new SystemLog { Id = 2, Source = "API", Level = DomainLogLevel.Error, Message = "Failed", CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetSystemLogsQuery(new PaginationParams(1, 20), "Error"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].Level.Should().Be("Error");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetSystemLogsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetSystemLogsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
