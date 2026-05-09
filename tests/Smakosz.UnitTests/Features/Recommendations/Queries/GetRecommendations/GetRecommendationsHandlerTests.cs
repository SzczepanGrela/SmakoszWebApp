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
    private readonly GetRecommendationsHandler _handler;

    public GetRecommendationsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _provider = Substitute.For<IRecommendationProvider>();
        _provider.IsAvailable.Returns(false);
        _provider.FallbackReason.Returns("Model NCF nie jest jeszcze dostępny.");
        _handler = new GetRecommendationsHandler(_db, _currentUser, _provider);
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
    public async Task Handle_ProviderAvailable_EnoughReviews_ReturnsPersonalized()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);
        _provider.GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<(int DishId, float Score)>
            {
                (10, 4.5f),
                (20, 3.8f)
            });

        var restaurant = new RestaurantBuilder().WithId(1).Build();
        _sets.Restaurants.Add(restaurant);

        var dish10 = new DishBuilder().WithId(10).WithName("NCF Dish 1").AsAvailable().Build();
        dish10.Restaurant = restaurant;
        var dish20 = new DishBuilder().WithId(20).WithName("NCF Dish 2").AsAvailable().Build();
        dish20.Restaurant = restaurant;
        _sets.Dishes.Add(dish10);
        _sets.Dishes.Add(dish20);

        for (var i = 1; i <= 10; i++)
        {
            _sets.Reviews.Add(new ReviewBuilder()
                .WithId(i)
                .WithUserId(1)
                .WithDishId(100 + i)
                .Build());
        }

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeTrue();
        result.Value.Personalized.Should().NotBeEmpty();
        result.Value.Personalized.Should().AllSatisfy(d => d.Source.Should().Be("ncf"));
    }

    [Fact]
    public async Task Handle_ProviderThrowsException_ReturnsFallbackGracefully()
    {
        _provider.IsAvailable.Returns(true);
        _provider.IsUserInMapping(1).Returns(true);
        _provider.GetPersonalizedAsync(1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns<List<(int DishId, float Score)>>(x => throw new InvalidOperationException("ONNX error"));

        for (var i = 1; i <= 10; i++)
        {
            _sets.Reviews.Add(new ReviewBuilder()
                .WithId(i)
                .WithUserId(1)
                .WithDishId(100 + i)
                .Build());
        }

        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRecommendationsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.NcfAvailable.Should().BeFalse();
        result.Value.FallbackReason.Should().Contain("błąd");
    }
}
