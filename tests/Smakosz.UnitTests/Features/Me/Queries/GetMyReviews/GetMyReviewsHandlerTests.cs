using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Queries.GetMyReviews;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Queries.GetMyReviews;

[Trait("Category", "Handlers")]
public class GetMyReviewsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetMyReviewsHandler _handler;

    public GetMyReviewsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new GetMyReviewsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsMyReviews()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        var review = new ReviewBuilder().WithId(1).WithUserId(1).WithDishId(1).WithRestaurantId(1)
            .WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetMyReviewsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetMyReviewsHandler(_db, anonymous);

        var result = await handler.Handle(
            new GetMyReviewsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
