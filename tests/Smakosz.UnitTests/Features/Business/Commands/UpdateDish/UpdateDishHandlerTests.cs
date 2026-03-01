using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateDish;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateDish;

[Trait("Category", "Handlers")]
public class UpdateDishHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateDishHandler _handler;

    public UpdateDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new UpdateDishHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesFields()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateDishCommand(dish.PublicId, "New Name", 19.99m, null, null, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.DishName.Should().Be("New Name");
        dish.Price.Should().Be(19.99m);
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 999;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateDishCommand(dish.PublicId, "X", null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new UpdateDishCommand(Guid.NewGuid(), "X", null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }
}
