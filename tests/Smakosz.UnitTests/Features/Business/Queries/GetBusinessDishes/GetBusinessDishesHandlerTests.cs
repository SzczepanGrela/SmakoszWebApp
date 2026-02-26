using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetBusinessDishes;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Queries.GetBusinessDishes;

[Trait("Category", "Handlers")]
public class GetBusinessDishesHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetBusinessDishesHandler _handler;

    public GetBusinessDishesHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _handler = new GetBusinessDishesHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_OwnerWithDishes_ReturnsDishesForRestaurant()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(new Dish { DishId = 1, RestaurantId = 1, DishName = "Pizza", Slug = "pizza", IsAvailable = true });
        _sets.Dishes.Add(new Dish { DishId = 2, RestaurantId = 1, DishName = "Pasta", Slug = "pasta", IsAvailable = false });
        _sets.Dishes.Add(new Dish { DishId = 3, RestaurantId = 99, DishName = "Other Dish", Slug = "other" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessDishesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Data.Should().AllSatisfy(d => d.DishId.Should().BeOneOf(1, 2));
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFound()
    {
        var result = await _handler.Handle(new GetBusinessDishesQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NoDishes_ReturnsEmptyList()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessDishesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DishDtoFields_MappedCorrectly()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(new Dish
        {
            DishId = 7,
            RestaurantId = 1,
            DishName = "Burger",
            Slug = "burger",
            Price = 25.99m,
            Description = "Classic beef burger",
            IsAvailable = true
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessDishesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var dish = result.Value.Data.Single();
        dish.DishId.Should().Be(7);
        dish.DishName.Should().Be("Burger");
        dish.Slug.Should().Be("burger");
        dish.Price.Should().Be(25.99m);
        dish.Description.Should().Be("Classic beef burger");
        dish.IsAvailable.Should().BeTrue();
    }
}
