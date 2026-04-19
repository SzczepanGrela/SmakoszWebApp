using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetAdminDishes;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetAdminDishes;

[Trait("Category", "Handlers")]
public class GetAdminDishesHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetAdminDishesHandler _handler;

    public GetAdminDishesHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetAdminDishesHandler(_db, _currentUser);
    }

    private static Restaurant CreateRestaurant(int id, string name) => new()
    {
        RestaurantId = id,
        PublicId = Guid.NewGuid(),
        RestaurantName = name,
        OwnerId = 1
    };

    private static Dish CreateDish(int id, string name, Restaurant restaurant, ContentModerationStatus status = ContentModerationStatus.Approved, bool isAvailable = true) => new()
    {
        DishId = id,
        PublicId = Guid.NewGuid(),
        DishName = name,
        RestaurantId = restaurant.RestaurantId,
        Restaurant = restaurant,
        ModerationStatus = status,
        IsAvailable = isAvailable,
        CreatedAt = DateTime.UtcNow.AddHours(-id)
    };

    [Fact]
    public async Task Handle_ReturnsPagedDishes_WhenAdminAuthorized()
    {
        var restaurant = CreateRestaurant(1, "Burger Bar");
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(CreateDish(1, "Pizza Margherita", restaurant));
        _sets.Dishes.Add(CreateDish(2, "Cheeseburger", restaurant));
        _sets.Dishes.Add(CreateDish(3, "Caesar Salad", restaurant));
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminDishesQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(3);
        result.Value.Data[0].RestaurantName.Should().Be("Burger Bar");
    }

    [Fact]
    public async Task Handle_FiltersBySearch_CaseInsensitive()
    {
        var restaurant = CreateRestaurant(1, "Pizzeria");
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(CreateDish(1, "Pizza Margherita", restaurant));
        _sets.Dishes.Add(CreateDish(2, "Cheeseburger", restaurant));
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminDishesQuery(new PaginationParams(1, 20), Search: "PIZZA"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].DishName.Should().Be("Pizza Margherita");
    }

    [Fact]
    public async Task Handle_FiltersByRestaurantId()
    {
        var r1 = CreateRestaurant(1, "R1");
        var r2 = CreateRestaurant(2, "R2");
        _sets.Restaurants.Add(r1);
        _sets.Restaurants.Add(r2);
        _sets.Dishes.Add(CreateDish(1, "D1", r1));
        _sets.Dishes.Add(CreateDish(2, "D2", r2));
        _sets.Dishes.Add(CreateDish(3, "D3", r1));
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminDishesQuery(new PaginationParams(1, 20), RestaurantId: 1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Data.Should().OnlyContain(d => d.RestaurantId == 1);
    }

    [Fact]
    public async Task Handle_FiltersByModerationStatus()
    {
        var restaurant = CreateRestaurant(1, "R");
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(CreateDish(1, "D1", restaurant, ContentModerationStatus.Approved));
        _sets.Dishes.Add(CreateDish(2, "D2", restaurant, ContentModerationStatus.Rejected));
        _sets.Dishes.Add(CreateDish(3, "D3", restaurant, ContentModerationStatus.Pending));
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminDishesQuery(new PaginationParams(1, 20), ModerationStatus: "Rejected"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].ModerationStatus.Should().Be("Rejected");
    }

    [Fact]
    public async Task Handle_FiltersByIsAvailable()
    {
        var restaurant = CreateRestaurant(1, "R");
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(CreateDish(1, "D1", restaurant, isAvailable: true));
        _sets.Dishes.Add(CreateDish(2, "D2", restaurant, isAvailable: false));
        _sets.Dishes.Add(CreateDish(3, "D3", restaurant, isAvailable: true));
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminDishesQuery(new PaginationParams(1, 20), IsAvailable: false), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetAdminDishesHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetAdminDishesQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
