using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.DeleteDish;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.DeleteDish;

[Trait("Category", "Handlers")]
public class DeleteDishHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly DeleteDishHandler _handler;

    public DeleteDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new DeleteDishHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_RemovesDish()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteDishCommand(dish.PublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Dishes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 999;
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteDishCommand(dish.PublicId), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new DeleteDishCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }
}
