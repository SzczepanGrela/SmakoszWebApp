using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetReports;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetReports;

[Trait("Category", "Handlers")]
public class GetReportsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetReportsHandler _handler;

    public GetReportsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _handler = new GetReportsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsPaginatedReports()
    {
        var reporter = new UserBuilder().WithId(1).WithUsername("reporter").Build();
        _sets.Users.Add(reporter);

        _sets.Reports.Add(new Report
        {
            ReportId = 1,
            ReporterId = 1,
            Reporter = reporter,
            EntityType = ReportEntityType.Review,
            EntityId = 10,
            Description = "Spam",
            Status = ReportStatus.Pending,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
        _sets.Reports.Add(new Report
        {
            ReportId = 2,
            ReporterId = 1,
            Reporter = reporter,
            EntityType = ReportEntityType.Review,
            EntityId = 11,
            Description = "Abusive",
            Status = ReportStatus.Resolved,
            CreatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetReportsQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetReportsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetReportsQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
