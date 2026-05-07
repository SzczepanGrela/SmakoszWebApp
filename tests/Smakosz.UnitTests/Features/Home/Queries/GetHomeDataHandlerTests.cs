using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Home.Queries.GetHomeData;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Home.Queries;

[Trait("Category", "Handlers")]
public class GetHomeDataHandlerTests : IDisposable
{
    private readonly SmakoszDbContext _db;
    private readonly GetHomeDataHandler _handler;
    private readonly DbContextOptions<SmakoszDbContext> _options;
    private readonly string _dbName = $"GetHomeData_{Guid.NewGuid():N}";

    public GetHomeDataHandlerTests()
    {
        _options = new DbContextOptionsBuilder<SmakoszDbContext>()
            .UseInMemoryDatabase(_dbName)
            .Options;

        _db = new SmakoszDbContext(_options);
        var factory = new TestDbContextFactory(_options);
        _handler = new GetHomeDataHandler(_db, factory);
    }

    public void Dispose() => _db.Dispose();

    private sealed class TestDbContextFactory : ISmakoszDbContextFactory
    {
        private readonly DbContextOptions<SmakoszDbContext> _options;
        public TestDbContextFactory(DbContextOptions<SmakoszDbContext> options) => _options = options;

        public Task<ISmakoszDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ISmakoszDbContext>(new SmakoszDbContext(_options));
    }

    private void SeedSiteStats(int dishes = 0, int restaurants = 0, int reviews = 0)
    {
        _db.SiteStats.Add(new SiteStats
        {
            Id = 1,
            TotalDishes = dishes,
            TotalRestaurants = restaurants,
            TotalReviews = reviews
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Handle_WithData_ReturnsAllSections()
    {
        SeedSiteStats(dishes: 1, restaurants: 1, reviews: 1);
        var city = new City { CityId = 1, CityName = "Warsaw" };
        var restaurant = new RestaurantBuilder()
            .WithId(1).WithCity(city).WithCuisineType("Italian").WithTrendingScore(100m).Build();
        var dish = new DishBuilder()
            .WithId(1).WithRestaurant(restaurant).WithTrendingScore(90m).WithReviewCount(5).WithAvgRating(9.0).Build();
        var user = new UserBuilder().WithId(1).Build();
        var review = new ReviewBuilder()
            .WithUser(user).WithDish(dish).WithRestaurant(restaurant).Build();

        _db.Restaurants.Add(restaurant);
        _db.Dishes.Add(dish);
        _db.Reviews.Add(review);
        _db.SaveChanges();

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
        SeedSiteStats();

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
        SeedSiteStats();
        var city = new City { CityId = 1, CityName = "Warsaw" };
        var cuisine = new CuisineType { CuisineTypeId = 1, Name = "Italian", DisplayName = "Italian" };
        var active = new RestaurantBuilder().WithId(1).WithCity(city).AsActive().Build();
        active.Cuisine = cuisine;
        var suspended = new RestaurantBuilder().WithId(2).WithCity(city).AsSuspended().Build();
        suspended.Cuisine = cuisine;
        _db.Restaurants.AddRange(active, suspended);
        _db.SaveChanges();

        var result = await _handler.Handle(new GetHomeDataQuery(), CancellationToken.None);

        result.Value.TrendingRestaurants.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_TopRated_RequiresMinimumThreeReviews()
    {
        SeedSiteStats();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dishFewReviews = new DishBuilder()
            .WithId(1).WithRestaurant(restaurant).WithAvgRating(10.0).WithReviewCount(2).Build();
        var dishEnoughReviews = new DishBuilder()
            .WithId(2).WithRestaurant(restaurant).WithAvgRating(8.0).WithReviewCount(3).Build();
        _db.Restaurants.Add(restaurant);
        _db.Dishes.AddRange(dishFewReviews, dishEnoughReviews);
        _db.SaveChanges();

        var result = await _handler.Handle(new GetHomeDataQuery(), CancellationToken.None);

        result.Value.TopRatedDishes.Should().HaveCount(1);
        result.Value.TopRatedDishes[0].DishName.Should().Be(dishEnoughReviews.DishName);
    }

    [Fact]
    public async Task Handle_PendingRestaurant_NotInTrending()
    {
        SeedSiteStats();
        var city = new City { CityId = 1, CityName = "Warsaw" };
        var cuisine = new CuisineType { CuisineTypeId = 1, Name = "Italian", DisplayName = "Italian" };
        var approved = new RestaurantBuilder().WithId(1).WithCity(city).AsActive().WithTrendingScore(100m).Build();
        approved.ModerationStatus = ContentModerationStatus.Approved;
        approved.Cuisine = cuisine;
        var pending = new RestaurantBuilder().WithId(2).WithCity(city).AsActive().WithTrendingScore(200m).Build();
        pending.ModerationStatus = ContentModerationStatus.Pending;
        pending.Cuisine = cuisine;
        _db.Restaurants.AddRange(approved, pending);
        _db.SaveChanges();

        var result = await _handler.Handle(new GetHomeDataQuery(), CancellationToken.None);

        result.Value.TrendingRestaurants.Should().HaveCount(1);
        result.Value.TrendingRestaurants[0].RestaurantName.Should().Be(approved.RestaurantName);
    }

    [Fact]
    public async Task Handle_PendingDish_NotInTrendingOrTopRated()
    {
        SeedSiteStats();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var approvedDish = new DishBuilder()
            .WithId(1).WithName("Approved").WithRestaurant(restaurant).WithTrendingScore(90m).WithReviewCount(5).WithAvgRating(9.0).Build();
        approvedDish.ModerationStatus = ContentModerationStatus.Approved;
        var pendingDish = new DishBuilder()
            .WithId(2).WithName("Pending").WithRestaurant(restaurant).WithTrendingScore(200m).WithReviewCount(5).WithAvgRating(10.0).Build();
        pendingDish.ModerationStatus = ContentModerationStatus.Pending;
        _db.Restaurants.Add(restaurant);
        _db.Dishes.AddRange(approvedDish, pendingDish);
        _db.SaveChanges();

        var result = await _handler.Handle(new GetHomeDataQuery(), CancellationToken.None);

        result.Value.TrendingDishes.Should().HaveCount(1);
        result.Value.TrendingDishes[0].DishName.Should().Be("Approved");
        result.Value.TopRatedDishes.Should().HaveCount(1);
        result.Value.TopRatedDishes[0].DishName.Should().Be("Approved");
    }
}
