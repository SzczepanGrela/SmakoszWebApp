using FluentAssertions;
using Smakosz.Application.Features.Restaurants.Queries.GetRestaurantBySlug;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Restaurants.Queries;

[Trait("Category", "Handlers")]
public class GetRestaurantBySlugHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetRestaurantBySlugHandler _handler;

    public GetRestaurantBySlugHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        _handler = new GetRestaurantBySlugHandler(_db, anonymousUser);
    }

    [Fact]
    public async Task Handle_ExistingSlug_ReturnsRestaurant()
    {
        var city = new City { CityId = 1, CityName = "Krakow" };
        var restaurant = new RestaurantBuilder().WithSlug("bella-italia").WithCity(city).Build();
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRestaurantBySlugQuery("bella-italia"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Slug.Should().Be("bella-italia");
        result.Value.CityName.Should().Be("Krakow");
    }

    [Fact]
    public async Task Handle_NonExistentSlug_ReturnsNotFound()
    {
        var result = await _handler.Handle(new GetRestaurantBySlugQuery("nonexistent"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AuthenticatedWithFavorite_IsFavoriteTrue()
    {
        var authUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        var handler = new GetRestaurantBySlugHandler(_db, authUser);
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("test-restaurant").Build();
        _sets.Restaurants.Add(restaurant);
        _sets.FavoriteRestaurants.Add(new FavoriteRestaurant { UserId = 1, RestaurantId = 1 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(new GetRestaurantBySlugQuery("test-restaurant"), CancellationToken.None);

        result.Value.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AuthenticatedNotFavorite_IsFavoriteFalse()
    {
        var authUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        var handler = new GetRestaurantBySlugHandler(_db, authUser);
        var restaurant = new RestaurantBuilder().WithSlug("test-restaurant").Build();
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(new GetRestaurantBySlugQuery("test-restaurant"), CancellationToken.None);

        result.Value.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Anonymous_IsFavoriteFalse()
    {
        var restaurant = new RestaurantBuilder().WithSlug("test-restaurant").Build();
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRestaurantBySlugQuery("test-restaurant"), CancellationToken.None);

        result.Value.IsFavorite.Should().BeFalse();
    }
}
