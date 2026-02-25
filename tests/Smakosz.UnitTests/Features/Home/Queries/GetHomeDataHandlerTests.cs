using FluentAssertions;
using Smakosz.Application.Features.Home.Queries.GetHomeData;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Home.Queries;

[Trait("Category", "Handlers")]
public class GetHomeDataHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetHomeDataHandler _handler;

    public GetHomeDataHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetHomeDataHandler(_db);
    }

    [Fact]
    public async Task Handle_WithData_ReturnsAllSections()
    {
        var city = new City { CityId = 1, CityName = "Warsaw" };
        var restaurant = new RestaurantBuilder()
            .WithId(1).WithCity(city).WithCuisineType("Italian").WithTrendingScore(100m).Build();
        var dish = new DishBuilder()
            .WithId(1).WithRestaurant(restaurant).WithTrendingScore(90m).WithReviewCount(5).WithAvgRating(9.0).Build();
        var user = new UserBuilder().WithId(1).Build();
        var review = new ReviewBuilder()
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();

        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        _sets.Reviews.Add(review);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetHomeDataQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Stats.TotalDishes.Should().Be(1);
        result.Value.Stats.TotalRestaurants.Should().Be(1);
        result.Value.Stats.TotalReviews.Should().Be(1);
        result.Value.TrendingRestaurants.Should().HaveCount(1);
        result.Value.TrendingDishes.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_EmptyDatabase_ReturnsZeroStats()
    {
        var result = await _handler.Handle(new GetHomeDataQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Stats.TotalDishes.Should().Be(0);
        result.Value.Stats.TotalRestaurants.Should().Be(0);
        result.Value.Stats.TotalReviews.Should().Be(0);
        result.Value.TrendingRestaurants.Should().BeEmpty();
        result.Value.TrendingDishes.Should().BeEmpty();
        result.Value.TopRatedDishes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OnlyActiveRestaurants_CountedInStats()
    {
        var active = new RestaurantBuilder().WithId(1).AsActive().Build();
        var suspended = new RestaurantBuilder().WithId(2).AsSuspended().Build();
        _sets.Restaurants.AddRange(new[] { active, suspended });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetHomeDataQuery(), CancellationToken.None);

        result.Value.Stats.TotalRestaurants.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TopRated_RequiresMinimumThreeReviews()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dishFewReviews = new DishBuilder()
            .WithId(1).WithRestaurant(restaurant).WithAvgRating(10.0).WithReviewCount(2).Build();
        var dishEnoughReviews = new DishBuilder()
            .WithId(2).WithRestaurant(restaurant).WithAvgRating(8.0).WithReviewCount(3).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { dishFewReviews, dishEnoughReviews });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetHomeDataQuery(), CancellationToken.None);

        result.Value.TopRatedDishes.Should().HaveCount(1);
        result.Value.TopRatedDishes[0].DishName.Should().Be(dishEnoughReviews.DishName);
    }
}
