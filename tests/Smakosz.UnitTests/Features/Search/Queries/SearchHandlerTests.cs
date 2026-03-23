using FluentAssertions;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Search.Queries.SearchQuery;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Search.Queries;

[Trait("Category", "Handlers")]
public class SearchHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly SearchHandler _handler;
    private readonly PaginationParams _defaultPagination = new(Page: 1, PageSize: 10);

    public SearchHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        _handler = new SearchHandler(_db, anonymousUser);
    }

    // Note: Tests with Query parameter (text search) are skipped because
    // EF.Functions.Like() throws InvalidOperationException outside EF query translation.
    // Only filter/sort/pagination paths are tested.

    [Fact]
    public async Task Handle_TypeRestaurants_ReturnsOnlyRestaurants()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "restaurants");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Restaurants.Should().HaveCount(1);
        result.Value.Dishes.Should().BeEmpty();
        result.Value.Type.Should().Be("restaurants");
    }

    [Fact]
    public async Task Handle_TypeDishes_ReturnsOnlyDishes()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "dishes");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Dishes.Should().HaveCount(1);
        result.Value.Restaurants.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TypeAll_ReturnsBoth()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "all");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Restaurants.Should().HaveCount(1);
        result.Value.Dishes.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_CuisineFilter_FiltersRestaurants()
    {
        var italian = new RestaurantBuilder().WithId(1).WithCuisineType("Italian").Build();
        var polish = new RestaurantBuilder().WithId(2).WithCuisineType("Polish").Build();
        _sets.Restaurants.AddRange(new[] { italian, polish });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "restaurants", Cuisines: "Italian");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Restaurants.Should().HaveCount(1);
        result.Value.Restaurants[0].CuisineType.Should().Be("Italian");
    }

    [Fact]
    public async Task Handle_CuisineFilter_FiltersDishes()
    {
        var italian = new RestaurantBuilder().WithId(1).WithCuisineType("Italian").Build();
        var polish = new RestaurantBuilder().WithId(2).WithCuisineType("Polish").Build();
        var italianDish = new DishBuilder().WithId(1).WithRestaurant(italian).Build();
        var polishDish = new DishBuilder().WithId(2).WithRestaurant(polish).Build();
        _sets.Restaurants.AddRange(new[] { italian, polish });
        _sets.Dishes.AddRange(new[] { italianDish, polishDish });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "dishes", Cuisines: "Italian");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Dishes.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_PriceFilter_FiltersRestaurants()
    {
        var cheap = new RestaurantBuilder().WithId(1).WithPriceLevel(1).Build();
        var expensive = new RestaurantBuilder().WithId(2).WithPriceLevel(4).Build();
        _sets.Restaurants.AddRange(new[] { cheap, expensive });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "restaurants", MinPrice: 2, MaxPrice: 5);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Restaurants.Should().HaveCount(1);
        result.Value.Restaurants[0].PriceLevel.Should().Be(4);
    }

    [Fact]
    public async Task Handle_DietaryVegan_FiltersDishes()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var vegan = new DishBuilder().WithId(1).WithRestaurant(restaurant).AsVegan().Build();
        var regular = new DishBuilder().WithId(2).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { vegan, regular });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "dishes", Dietary: "vegan");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Dishes.Should().HaveCount(1);
        result.Value.Dishes[0].IsVegan.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DietaryGlutenFree_FiltersDishes()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var gf = new DishBuilder().WithId(1).WithRestaurant(restaurant).AsGlutenFree().Build();
        var regular = new DishBuilder().WithId(2).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { gf, regular });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "dishes", Dietary: "gluten_free");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Dishes.Should().HaveCount(1);
        result.Value.Dishes[0].IsGlutenFree.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SortByNameAsc_ReturnsSorted()
    {
        var r1 = new RestaurantBuilder().WithId(1).WithName("Zebra").Build();
        var r2 = new RestaurantBuilder().WithId(2).WithName("Amber").Build();
        _sets.Restaurants.AddRange(new[] { r1, r2 });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "restaurants", SortBy: "name", SortDir: "asc");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Restaurants[0].RestaurantName.Should().Be("Amber");
        result.Value.Restaurants[1].RestaurantName.Should().Be("Zebra");
    }

    [Fact]
    public async Task Handle_SortByPriceAsc_ReturnsSorted()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        var expensive = new DishBuilder().WithId(1).WithRestaurant(restaurant).WithPrice(50m).Build();
        var cheap = new DishBuilder().WithId(2).WithRestaurant(restaurant).WithPrice(10m).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { expensive, cheap });
        DbContextMockFactory.Refresh(_db, _sets);
        var query = new SearchQuery(_defaultPagination, Type: "dishes", SortBy: "price", SortDir: "asc");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Dishes[0].Price.Should().Be(10m);
        result.Value.Dishes[1].Price.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_Pagination_ReturnsCorrectInfo()
    {
        for (int i = 1; i <= 15; i++)
        {
            _sets.Restaurants.Add(new RestaurantBuilder().WithId(i).WithName($"Rest{i:D2}").Build());
        }
        DbContextMockFactory.Refresh(_db, _sets);
        var pagination = new PaginationParams(Page: 2, PageSize: 10);
        var query = new SearchQuery(pagination, Type: "restaurants");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.Restaurants.Should().HaveCount(5);
        result.Value.Pagination.TotalCount.Should().Be(15);
        result.Value.Pagination.TotalPages.Should().Be(2);
        result.Value.Pagination.Page.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AppliedFilters_ReturnedCorrectly()
    {
        var query = new SearchQuery(_defaultPagination, Type: "restaurants", Cuisines: "Italian,Polish", Dietary: "vegan");

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Value.AppliedFilters.Type.Should().Be("restaurants");
        result.Value.AppliedFilters.Cuisines.Should().Contain("Italian").And.Contain("Polish");
        result.Value.AppliedFilters.Dietary.Should().Contain("vegan");
    }
}
