using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Recommendations.Queries.GetRecommendations;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Recommendations.Queries.GetRecommendations;

[Trait("Category", "Handlers")]
public class GetRecommendationsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetRecommendationsHandler _handler;

    public GetRecommendationsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new GetRecommendationsHandler(_db, _currentUser);
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
    public async Task Handle_NcfAvailableIsFalse()
    {
        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.FallbackReason.Should().NotBeNullOrEmpty();
    }
}
