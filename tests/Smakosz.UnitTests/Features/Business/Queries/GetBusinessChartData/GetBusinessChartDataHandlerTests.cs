using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetBusinessChartData;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Queries.GetBusinessChartData;

[Trait("Category", "Handlers")]
public class GetBusinessChartDataHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetBusinessChartDataHandler _handler;

    public GetBusinessChartDataHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new GetBusinessChartDataHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsChartData()
    {
        var restaurant = new RestaurantBuilder()
            .WithId(1)
            .WithAvgFoodScore(8.0)
            .Build();
        restaurant.OwnerId = 1;
        restaurant.AvgService = 7.5;
        restaurant.AvgCleanliness = 9.0;
        restaurant.AvgAmbiance = 7.0;

        _sets.Restaurants.Add(restaurant);

        _sets.Reviews.Add(new ReviewBuilder().WithId(1).WithRestaurantId(1)
            .WithDishRating(8).WithCreatedAt(DateTime.UtcNow.AddDays(-5)).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(2).WithRestaurantId(1)
            .WithDishRating(6).WithCreatedAt(DateTime.UtcNow.AddDays(-2)).Build());

        var dish = new DishBuilder().WithId(1).WithRestaurantId(1)
            .WithAvgRating(8.5).WithReviewCount(5).Build();
        _sets.Dishes.Add(dish);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessChartDataQuery(30), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ReviewTrend.Should().NotBeEmpty();
        result.Value.RatingDistribution.Should().HaveCount(10);
        result.Value.CategoryAverages.Food.Should().Be(8.0);
        result.Value.CategoryAverages.Service.Should().Be(7.5);
        result.Value.CategoryAverages.Cleanliness.Should().Be(9.0);
        result.Value.CategoryAverages.Ambiance.Should().Be(7.0);
        result.Value.TopDishes.Should().HaveCount(1);
        result.Value.TopDishes[0].DishName.Should().Be("Test Dish");
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsError()
    {
        var result = await _handler.Handle(new GetBusinessChartDataQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_EmptyData_ReturnsEmptyCharts()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        restaurant.AvgFoodScore = null;
        restaurant.AvgService = null;
        restaurant.AvgCleanliness = null;
        restaurant.AvgAmbiance = null;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessChartDataQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.ReviewTrend.Should().NotBeEmpty(); // days filled with zeros
        result.Value.ReviewTrend.Should().AllSatisfy(d => d.Count.Should().Be(0));
        result.Value.RatingDistribution.Should().HaveCount(10);
        result.Value.RatingDistribution.Should().AllSatisfy(r => r.Count.Should().Be(0));
        result.Value.CategoryAverages.Food.Should().Be(0);
        result.Value.CategoryAverages.Service.Should().Be(0);
        result.Value.TopDishes.Should().BeEmpty();
    }
}
