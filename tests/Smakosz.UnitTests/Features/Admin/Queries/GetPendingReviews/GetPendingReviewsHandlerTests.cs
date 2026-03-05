using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetPendingReviews;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetPendingReviews;

[Trait("Category", "Handlers")]
public class GetPendingReviewsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetPendingReviewsHandler _handler;

    public GetPendingReviewsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetPendingReviewsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPendingReviews()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        var review = new ReviewBuilder().WithId(1).WithUserId(1).WithDishId(1).WithRestaurantId(1)
            .WithContentStatus(ContentModerationStatus.Pending)
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetPendingReviewsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NeedsReviewReview_AppearsInResults()
    {
        var user = new UserBuilder().WithId(2).Build();
        var restaurant = new RestaurantBuilder().WithId(2).Build();
        var dish = new DishBuilder().WithId(2).WithRestaurant(restaurant).Build();
        var review = new ReviewBuilder().WithId(2).WithUserId(2).WithDishId(2).WithRestaurantId(2)
            .WithContentStatus(ContentModerationStatus.NeedsReview)
            .WithIsApproved(null)
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetPendingReviewsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetPendingReviewsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetPendingReviewsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
