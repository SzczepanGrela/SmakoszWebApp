using FluentAssertions;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Reviews.Queries.GetReviewsByDish;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Reviews.Queries;

[Trait("Category", "Handlers")]
public class GetReviewsByDishHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetReviewsByDishHandler _handler;
    private readonly PaginationParams _defaultPagination = new(Page: 1, PageSize: 10);

    public GetReviewsByDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        _handler = new GetReviewsByDishHandler(_db, anonymousUser);
    }

    [Fact]
    public async Task Handle_ExistingDish_ReturnsReviews()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithSlug("margherita").Build();
        var review = new ReviewBuilder()
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetReviewsByDishQuery("margherita", _defaultPagination), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_DishNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new GetReviewsByDishQuery("nonexistent", _defaultPagination), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_SortByNewest_DefaultSort()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithSlug("test-dish").Build();
        var older = new ReviewBuilder()
            .WithId(1).WithUser(user).WithDish(dish).WithRestaurant(restaurant)
            .WithCreatedAt(DateTime.UtcNow.AddDays(-2)).Build();
        var newer = new ReviewBuilder()
            .WithId(2).WithUser(user).WithDish(dish).WithRestaurant(restaurant)
            .WithCreatedAt(DateTime.UtcNow).Build();
        _sets.Dishes.Add(dish);
        _sets.Reviews.AddRange(new[] { older, newer });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetReviewsByDishQuery("test-dish", _defaultPagination, SortBy: "newest"), CancellationToken.None);

        result.Value.Data[0].PublicId.Should().Be(newer.PublicId);
    }

    [Fact]
    public async Task Handle_SortByHelpful_ReturnsHighestFirst()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithSlug("test-dish").Build();
        var lessHelpful = new ReviewBuilder()
            .WithId(1).WithUser(user).WithDish(dish).WithRestaurant(restaurant)
            .WithHelpfulCount(2).Build();
        var moreHelpful = new ReviewBuilder()
            .WithId(2).WithUser(user).WithDish(dish).WithRestaurant(restaurant)
            .WithHelpfulCount(10).Build();
        _sets.Dishes.Add(dish);
        _sets.Reviews.AddRange(new[] { lessHelpful, moreHelpful });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetReviewsByDishQuery("test-dish", _defaultPagination, SortBy: "helpful"), CancellationToken.None);

        result.Value.Data[0].HelpfulCount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_LikedReviewsMarked()
    {
        var authUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        var handler = new GetReviewsByDishHandler(_db, authUser);
        var user = new UserBuilder().WithId(2).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithSlug("test-dish").Build();
        var review = new ReviewBuilder()
            .WithId(1).WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        _sets.Reviews.Add(review);
        _sets.ReviewLikes.Add(new ReviewLike { UserId = 1, ReviewId = 1 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(
            new GetReviewsByDishQuery("test-dish", _defaultPagination), CancellationToken.None);

        result.Value.Data[0].IsHelpfulByMe.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Anonymous_IsHelpfulByMeFalse()
    {
        var user = new UserBuilder().WithId(1).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithSlug("test-dish").Build();
        var review = new ReviewBuilder()
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetReviewsByDishQuery("test-dish", _defaultPagination), CancellationToken.None);

        result.Value.Data[0].IsHelpfulByMe.Should().BeFalse();
    }
}
