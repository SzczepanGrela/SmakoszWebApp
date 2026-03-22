using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Business.Queries.GetBusinessReviews;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Queries.GetBusinessReviews;

[Trait("Category", "Handlers")]
public class GetBusinessReviewsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidationConfigProvider _config;
    private readonly GetBusinessReviewsHandler _handler;

    public GetBusinessReviewsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _config = new StubValidationConfigProvider();
        _handler = new GetBusinessReviewsHandler(_db, _currentUser, _config);
    }

    [Fact]
    public async Task Handle_ReturnsPagedReviews()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var user = new UserBuilder().WithId(2).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        var review = new ReviewBuilder().WithId(1).WithUserId(2).WithDishId(1).WithRestaurantId(1)
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetBusinessReviewsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsError()
    {
        var result = await _handler.Handle(
            new GetBusinessReviewsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetBusinessReviewsHandler(_db, anonymous, _config);

        var result = await handler.Handle(
            new GetBusinessReviewsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
