using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetAdminDashboard;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
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
        _sets.SiteStats.Add(new SiteStats { Id = 1, TotalUsers = 2, TotalRestaurants = 1, TotalReviews = 2 });
        _sets.Reports.Add(new Report { ReportId = 1, ReporterId = 1, EntityType = ReportEntityType.Review, EntityId = 1, Status = ReportStatus.Pending });
        _sets.Reports.Add(new Report { ReportId = 2, ReporterId = 2, EntityType = ReportEntityType.Review, EntityId = 2, Status = ReportStatus.Resolved });
        _sets.MediaAssets.Add(new MediaAsset { AssetId = 1, Status = MediaAssetStatus.Pending, Url = "http://img.jpg" });
        _sets.Reviews.Add(new ReviewBuilder().WithId(1).WithContentStatus(ReviewContentStatus.Pending).Build());
        _sets.SystemTickets.Add(new SystemTicket { TicketId = 1, TicketType = TicketType.Contact, Status = TicketStatus.Open, Priority = 3 });
        _sets.SystemTickets.Add(new SystemTicket { TicketId = 2, TicketType = TicketType.Photo, Status = TicketStatus.Resolved, Priority = 2 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalUsers.Should().Be(2);
        result.Value.TotalRestaurants.Should().Be(1);
        result.Value.TotalReviews.Should().Be(2);
        result.Value.PendingReports.Should().Be(1);
        result.Value.PendingPhotos.Should().Be(1);
        result.Value.PendingReviews.Should().Be(1);
        result.Value.OpenTickets.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Moderator_ReturnsData()
    {
        var moderator = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "Moderator");
        var handler = new GetAdminDashboardHandler(_db, moderator);

        _sets.SiteStats.Add(new SiteStats { Id = 1, TotalUsers = 5, TotalRestaurants = 3, TotalReviews = 10 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(new GetAdminDashboardQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalUsers.Should().Be(5);
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
