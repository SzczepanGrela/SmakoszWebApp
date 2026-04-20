using FluentAssertions;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Restaurants.Queries.GetRestaurants;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Restaurants.Queries;

[Trait("Category", "Handlers")]
public class GetRestaurantsHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly Smakosz.Application.Common.Interfaces.ICurrentUserService _currentUser;
    private readonly GetRestaurantsHandler _handler;
    private readonly PaginationParams _defaultPagination = new(Page: 1, PageSize: 10);

    public GetRestaurantsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAnonymousUser();
        _handler = new GetRestaurantsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_NoFilters_ReturnsActiveRestaurants()
    {
        var active = new RestaurantBuilder().WithId(1).AsActive().Build();
        var suspended = new RestaurantBuilder().WithId(2).AsSuspended().Build();
        _sets.Restaurants.AddRange(new[] { active, suspended });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new GetRestaurantsQuery(_defaultPagination);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_CityFilter_ReturnsMatchingOnly()
    {
        var city = new City { CityId = 1, CityName = "Warsaw" };
        var inCity = new RestaurantBuilder().WithId(1).WithCity(city).Build();
        var otherCity = new RestaurantBuilder().WithId(2).WithCityId(99).Build();
        _sets.Restaurants.AddRange(new[] { inCity, otherCity });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new GetRestaurantsQuery(_defaultPagination, CityId: 1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].PublicId.Should().Be(inCity.PublicId);
    }

    [Fact]
    public async Task Handle_CuisineFilter_ReturnsMatchingOnly()
    {
        var italian = new RestaurantBuilder().WithId(1).WithCuisineTypeId(1).Build();
        italian.Cuisine = new CuisineType { CuisineTypeId = 1, Name = "Italian", DisplayName = "Italian" };
        var polish = new RestaurantBuilder().WithId(2).WithCuisineTypeId(2).Build();
        polish.Cuisine = new CuisineType { CuisineTypeId = 2, Name = "Polish", DisplayName = "Polish" };
        _sets.Restaurants.AddRange(new[] { italian, polish });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new GetRestaurantsQuery(_defaultPagination, CuisineTypeId: 1);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].CuisineType.Should().Be("Italian");
    }

    [Fact]
    public async Task Handle_PriceFilter_ReturnsInRange()
    {
        var cheap = new RestaurantBuilder().WithId(1).WithPriceLevel(1).Build();
        var mid = new RestaurantBuilder().WithId(2).WithPriceLevel(2).Build();
        var expensive = new RestaurantBuilder().WithId(3).WithPriceLevel(4).Build();
        _sets.Restaurants.AddRange(new[] { cheap, mid, expensive });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new GetRestaurantsQuery(_defaultPagination, MinPrice: 2, MaxPrice: 3);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].PriceLevel.Should().Be(2);
    }

    [Fact]
    public async Task Handle_SortByName_ReturnsSortedAlphabetically()
    {
        var b = new RestaurantBuilder().WithId(1).WithName("Bella").Build();
        var a = new RestaurantBuilder().WithId(2).WithName("Amber").Build();
        _sets.Restaurants.AddRange(new[] { b, a });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new GetRestaurantsQuery(_defaultPagination, SortBy: "name");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Data[0].RestaurantName.Should().Be("Amber");
        result.Value.Data[1].RestaurantName.Should().Be("Bella");
    }

    [Fact]
    public async Task Handle_SortByRating_ReturnsHighestFirst()
    {
        var low = new RestaurantBuilder().WithId(1).WithAvgFoodScore(5.0).Build();
        var high = new RestaurantBuilder().WithId(2).WithAvgFoodScore(9.0).Build();
        _sets.Restaurants.AddRange(new[] { low, high });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new GetRestaurantsQuery(_defaultPagination, SortBy: "rating");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Data[0].AvgFoodScore.Should().Be(9.0);
    }

    [Fact]
    public async Task Handle_DefaultSortByTrending_ReturnsHighestFirst()
    {
        var lowTrend = new RestaurantBuilder().WithId(1).WithTrendingScore(10m).Build();
        var highTrend = new RestaurantBuilder().WithId(2).WithTrendingScore(100m).Build();
        _sets.Restaurants.AddRange(new[] { lowTrend, highTrend });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new GetRestaurantsQuery(_defaultPagination);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Data[0].PublicId.Should().Be(highTrend.PublicId);
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_FavoritesMarked()
    {
        var authUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        var handler = new GetRestaurantsHandler(_db, authUser);
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.FavoriteRestaurants.Add(new FavoriteRestaurant { UserId = 1, RestaurantId = 1 });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new GetRestaurantsQuery(_defaultPagination);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Value.Data[0].IsFavorite.Should().BeTrue();
    }
}
