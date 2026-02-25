using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetAdminDashboard;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetAdminDashboard;

[Trait("Category", "Handlers")]
public class GetAdminDashboardHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetAdminDashboardHandler _handler;

    public GetAdminDashboardHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _handler = new GetAdminDashboardHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsCorrectCounts()
    {
        _sets.Users.Add(new UserBuilder().WithId(1).Build());
        _sets.Users.Add(new UserBuilder().WithId(2).Build());
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(1).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(1).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(2).Build());
        _sets.Reports.Add(new Report { ReportId = 1, ReporterId = 1, EntityType = ReportEntityType.Review, EntityId = 1, Status = ReportStatus.Pending });
        _sets.Reports.Add(new Report { ReportId = 2, ReporterId = 2, EntityType = ReportEntityType.Review, EntityId = 2, Status = ReportStatus.Resolved });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalUsers.Should().Be(2);
        result.Value.TotalRestaurants.Should().Be(1);
        result.Value.TotalReviews.Should().Be(2);
        result.Value.PendingReports.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetAdminDashboardHandler(_db, nonAdmin);

        var result = await handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
