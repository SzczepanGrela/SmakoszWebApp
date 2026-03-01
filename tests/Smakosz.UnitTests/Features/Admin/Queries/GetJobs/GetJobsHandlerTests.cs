using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetJobs;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetJobs;

[Trait("Category", "Handlers")]
public class GetJobsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetJobsHandler _handler;

    public GetJobsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetJobsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedJobs()
    {
        _sets.SystemJobs.Add(new SystemJob { JobId = 1, Type = "text_moderation", Status = JobStatus.Pending, CreatedAt = DateTime.UtcNow });
        _sets.SystemJobs.Add(new SystemJob { JobId = 2, Type = "image_moderation", Status = JobStatus.Completed, CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetJobsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetJobsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetJobsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
