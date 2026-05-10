using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Recommendations.Queries.GetRecommendations;
using Smakosz.Domain.Entities.System;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Recommendations.Queries.GetRecommendations;

[Trait("Category", "Handlers")]
public class GetRecommendationsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IRecommendationProvider _provider;
    private readonly IBusinessMetrics _metrics;
    private readonly GetRecommendationsHandler _handler;

    public GetRecommendationsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _provider = Substitute.For<IRecommendationProvider>();
        _provider.IsAvailable.Returns(false);
        _provider.FallbackReason.Returns("Model NCF nie jest jeszcze dostępny.");
        _metrics = Substitute.For<IBusinessMetrics>();
        _handler = new GetRecommendationsHandler(_db, _currentUser, _provider, _metrics);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsTrendingDishes()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        _sets.Restaurants.Add(restaurant);

        var dish1 = new DishBuilder()
            .WithId(1)
            .WithName("Popular Dish")
            .WithTrendingScore(100m)
            .AsAvailable()
            .Build();
        dish1.Restaurant = restaurant;

        var dish2 = new DishBuilder()
            .WithId(2)
            .WithName("Less Popular Dish")
            .WithTrendingScore(50m)
            .AsAvailable()
            .Build();
        dish2.Restaurant = restaurant;

        _sets.Dishes.Add(dish1);
        _sets.Dishes.Add(dish2);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Trending.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_ProviderUnavailable_NcfAvailableIsFalse()
    {
        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.FallbackReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ProviderAvailable_TooFewReviews_ReturnsFallbackReason()
    {
        _provider.IsAvailable.Returns(true);
        _provider.FallbackReason.Returns((string?)null);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.FallbackReason.Should().Contain("recenzji");
    }

    [Fact]
    public async Task Handle_CacheExists_ReturnsPersonalizedFromCache()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);

        var restaurant = new RestaurantBuilder().WithId(1).Build();
        _sets.Restaurants.Add(restaurant);

        var dish10 = new DishBuilder().WithId(10).WithName("NCF Dish 1").AsAvailable().Build();
        dish10.Restaurant = restaurant;
        var dish20 = new DishBuilder().WithId(20).WithName("NCF Dish 2").AsAvailable().Build();
        dish20.Restaurant = restaurant;
        _sets.Dishes.Add(dish10);
        _sets.Dishes.Add(dish20);

        _sets.UserRecommendationCaches.Add(new UserRecommendationCache
        {
            UserId = 1,
            TopDishIdsJson = "[{\"dishId\":10,\"score\":4.5},{\"dishId\":20,\"score\":3.8}]",
            ModelVersion = "v20260513_000000",
            GeneratedAt = DateTime.UtcNow
        });

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.Personalized.Should().HaveCount(2);
        result.Value.Personalized.Should().AllSatisfy(d => d.Source.Should().Be("ncf"));
        result.Value.Personalized.First().DishId.Should().Be(10);
    }

    [Fact]
    public async Task Handle_CacheEmpty_UserInMapping_FallbackReason()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.Personalized.Should().BeEmpty();
        result.Value.FallbackReason.Should().Contain("generowane");
    }

    [Fact]
    public async Task Handle_CacheExists_FiltersReviewedDishes()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);

        var restaurant = new RestaurantBuilder().WithId(1).Build();
        _sets.Restaurants.Add(restaurant);

        foreach (var id in new[] { 1, 2, 3, 4, 5 })
        {
            var d = new DishBuilder().WithId(id).WithName($"Dish {id}").AsAvailable().Build();
            d.Restaurant = restaurant;
            _sets.Dishes.Add(d);
        }

        _sets.Reviews.Add(new ReviewBuilder().WithId(1).WithUserId(1).WithDishId(2).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(2).WithUserId(1).WithDishId(4).Build());

        _sets.UserRecommendationCaches.Add(new UserRecommendationCache
        {
            UserId = 1,
            TopDishIdsJson = "[{\"dishId\":1,\"score\":4.9},{\"dishId\":2,\"score\":4.8},{\"dishId\":3,\"score\":4.7},{\"dishId\":4,\"score\":4.6},{\"dishId\":5,\"score\":4.5}]",
            ModelVersion = "v20260513_000000",
            GeneratedAt = DateTime.UtcNow
        });

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.Personalized.Select(p => p.DishId).Should().BeEquivalentTo(new[] { 1, 3, 5 });
    }
}
