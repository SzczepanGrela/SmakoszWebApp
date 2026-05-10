using FluentAssertions;
using Microsoft.Extensions.Logging;
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
    private const string CurrentModelVersion = "v20260515_000000";

    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IRecommendationProvider _provider;
    private readonly IBusinessMetrics _metrics;
    private readonly ILogger<GetRecommendationsHandler> _logger;
    private readonly GetRecommendationsHandler _handler;

    public GetRecommendationsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _provider = Substitute.For<IRecommendationProvider>();
        _provider.IsAvailable.Returns(false);
        _provider.FallbackReason.Returns("Model NCF nie jest jeszcze dostępny.");
        _provider.GetLoadedVersion().Returns(CurrentModelVersion);
        _metrics = Substitute.For<IBusinessMetrics>();
        _logger = Substitute.For<ILogger<GetRecommendationsHandler>>();
        _handler = new GetRecommendationsHandler(_db, _currentUser, _provider, _metrics, _logger);
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
        _metrics.Received().RecordRecommendationCacheLookup("provider_unavailable");
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
        _metrics.Received().RecordRecommendationCacheLookup("newcomer");
    }

    [Fact]
    public async Task Handle_CacheExists_MatchingVersion_ReturnsPersonalizedFromCache()
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

        SeedUserReviews(userId: 1, dishIds: [100, 101, 102, 103, 104]);

        _sets.UserRecommendationCaches.Add(new UserRecommendationCache
        {
            UserId = 1,
            TopDishIdsJson = "[{\"dishId\":10,\"score\":4.5},{\"dishId\":20,\"score\":3.8}]",
            ModelVersion = CurrentModelVersion,
            GeneratedAt = DateTime.UtcNow
        });

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.Personalized.Should().HaveCount(2);
        result.Value.Personalized.Should().AllSatisfy(d => d.Source.Should().Be("ncf"));
        result.Value.Personalized.First().DishId.Should().Be(10);
        _metrics.Received().RecordRecommendationCacheLookup("hit");
        await _provider.DidNotReceive().GetPersonalizedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CacheMiss_TriggersLazyComputeAndWritesCache()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);
        _provider.GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int DishId, float Score)>
            {
                (10, 4.9f),
                (20, 4.5f)
            });

        var restaurant = new RestaurantBuilder().WithId(1).Build();
        _sets.Restaurants.Add(restaurant);
        var dish10 = new DishBuilder().WithId(10).WithName("Computed 10").AsAvailable().Build();
        dish10.Restaurant = restaurant;
        var dish20 = new DishBuilder().WithId(20).WithName("Computed 20").AsAvailable().Build();
        dish20.Restaurant = restaurant;
        _sets.Dishes.Add(dish10);
        _sets.Dishes.Add(dish20);

        SeedUserReviews(userId: 1, dishIds: [100, 101, 102, 103, 104]);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.Personalized.Should().HaveCount(2);
        _metrics.Received().RecordRecommendationCacheLookup("cold_computed");
        await _provider.Received(1).GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>());
        _sets.UserRecommendationCaches.Should().ContainSingle(c =>
            c.UserId == 1 && c.ModelVersion == CurrentModelVersion);
    }

    [Fact]
    public async Task Handle_CacheStaleVersion_RecomputesAndOverwritesRow()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);
        _provider.GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int DishId, float Score)> { (30, 4.7f) });

        var restaurant = new RestaurantBuilder().WithId(1).Build();
        _sets.Restaurants.Add(restaurant);
        var dish30 = new DishBuilder().WithId(30).WithName("Fresh 30").AsAvailable().Build();
        dish30.Restaurant = restaurant;
        _sets.Dishes.Add(dish30);

        _sets.UserRecommendationCaches.Add(new UserRecommendationCache
        {
            UserId = 1,
            TopDishIdsJson = "[{\"dishId\":99,\"score\":3.0}]",
            ModelVersion = "v_OLD",
            GeneratedAt = DateTime.UtcNow.AddDays(-7)
        });

        SeedUserReviews(userId: 1, dishIds: [100, 101, 102, 103, 104]);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.Personalized.Should().ContainSingle().Which.DishId.Should().Be(30);
        _metrics.Received().RecordRecommendationCacheLookup("cold_computed");
        var existing = _sets.UserRecommendationCaches.Single(c => c.UserId == 1);
        existing.ModelVersion.Should().Be(CurrentModelVersion);
    }

    [Fact]
    public async Task Handle_ProviderThrows_ReturnsGracefulFallback()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);
        _provider.GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<Task<List<(int DishId, float Score)>>>(_ => throw new InvalidOperationException("onnx failed"));

        SeedUserReviews(userId: 1, dishIds: [100, 101, 102, 103, 104]);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.Personalized.Should().BeEmpty();
        result.Value.FallbackReason.Should().NotBeNullOrEmpty();
        _metrics.Received().RecordRecommendationCacheLookup("compute_failed");
    }

    [Fact]
    public async Task Handle_CacheHit_FiltersReviewedDishes()
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
        _sets.Reviews.Add(new ReviewBuilder().WithId(3).WithUserId(1).WithDishId(100).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(4).WithUserId(1).WithDishId(101).Build());
        _sets.Reviews.Add(new ReviewBuilder().WithId(5).WithUserId(1).WithDishId(102).Build());

        _sets.UserRecommendationCaches.Add(new UserRecommendationCache
        {
            UserId = 1,
            TopDishIdsJson = "[{\"dishId\":1,\"score\":4.9},{\"dishId\":2,\"score\":4.8},{\"dishId\":3,\"score\":4.7},{\"dishId\":4,\"score\":4.6},{\"dishId\":5,\"score\":4.5}]",
            ModelVersion = CurrentModelVersion,
            GeneratedAt = DateTime.UtcNow
        });

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.Personalized.Select(p => p.DishId).Should().BeEquivalentTo(new[] { 1, 3, 5 });
    }

    [Fact]
    public async Task Handle_UserHasLessThan5Reviews_ReturnsNewcomerEvenIfInMapping()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);
        _provider.FallbackReason.Returns((string?)null);

        SeedUserReviews(userId: 1, dishIds: [100, 101, 102]);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.IsNewcomer.Should().BeTrue();
        result.Value.Personalized.Should().BeEmpty();
        result.Value.FallbackReason.Should().Contain("5");
        _metrics.Received().RecordRecommendationCacheLookup("newcomer");
        await _provider.DidNotReceive().GetPersonalizedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserHasExactly5Reviews_AndInMapping_ProceedsToCompute()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);
        _provider.GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int DishId, float Score)> { (10, 4.8f) });

        var restaurant = new RestaurantBuilder().WithId(1).Build();
        _sets.Restaurants.Add(restaurant);
        var dish10 = new DishBuilder().WithId(10).WithName("Boundary Dish").AsAvailable().Build();
        dish10.Restaurant = restaurant;
        _sets.Dishes.Add(dish10);

        SeedUserReviews(userId: 1, dishIds: [100, 101, 102, 103, 104]);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.IsNewcomer.Should().BeFalse();
        result.Value.Personalized.Should().ContainSingle().Which.DishId.Should().Be(10);
        await _provider.Received(1).GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserHasManyReviews_NotInMapping_ReturnsNewcomer()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(false);
        _provider.FallbackReason.Returns((string?)null);

        SeedUserReviews(userId: 1, dishIds: [100, 101, 102, 103, 104, 105, 106, 107, 108, 109]);

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.IsNewcomer.Should().BeTrue();
        result.Value.Personalized.Should().BeEmpty();
        _metrics.Received().RecordRecommendationCacheLookup("newcomer");
        await _provider.DidNotReceive().GetPersonalizedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    private void SeedUserReviews(int userId, int[] dishIds)
    {
        var nextId = _sets.Reviews.Count == 0 ? 1 : _sets.Reviews.Max(r => r.ReviewId) + 1;
        foreach (var dishId in dishIds)
        {
            _sets.Reviews.Add(new ReviewBuilder().WithId(nextId++).WithUserId(userId).WithDishId(dishId).Build());
        }
    }
}
