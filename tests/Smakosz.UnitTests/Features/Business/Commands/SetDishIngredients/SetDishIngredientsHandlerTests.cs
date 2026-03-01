using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.SetDishIngredients;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.SetDishIngredients;

[Trait("Category", "Handlers")]
public class SetDishIngredientsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly SetDishIngredientsHandler _handler;

    public SetDishIngredientsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new SetDishIngredientsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_SetsIngredientsAndRecalculates()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        dish.DishIngredients = new List<DishIngredient>();
        _sets.Dishes.Add(dish);
        _sets.Ingredients.Add(new Ingredient { IngredientId = 1, IngredientName = "Salt", IsVegetarian = true, IsVegan = true, IsGlutenFree = true, IsLactoseFree = true });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new SetDishIngredientsCommand(dish.PublicId, new List<int> { 1 }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.DishIngredients.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_InvalidIngredientId_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        dish.DishIngredients = new List<DishIngredient>();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new SetDishIngredientsCommand(dish.PublicId, new List<int> { 999 }), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("INVALID_INGREDIENTS");
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 999;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        dish.DishIngredients = new List<DishIngredient>();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new SetDishIngredientsCommand(dish.PublicId, new List<int> { 1 }), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new SetDishIngredientsCommand(Guid.NewGuid(), new List<int> { 1 }), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }
}
