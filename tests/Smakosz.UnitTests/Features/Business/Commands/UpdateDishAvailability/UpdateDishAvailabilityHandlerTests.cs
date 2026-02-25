using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateDishAvailability;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateDishAvailability;

[Trait("Category", "Handlers")]
public class UpdateDishAvailabilityHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateDishAvailabilityHandler _handler;
    private static readonly Guid TestPublicId = Guid.NewGuid();

    public UpdateDishAvailabilityHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _handler = new UpdateDishAvailabilityHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_OwnedDish_TogglesAvailability()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "R", Slug = "r" };
        var dish = new Dish { DishId = 5, PublicId = TestPublicId, RestaurantId = 1, DishName = "D", Slug = "d", IsAvailable = true, Restaurant = restaurant };
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateDishAvailabilityCommand(TestPublicId, false), CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 99, RestaurantName = "R", Slug = "r" };
        var dish = new Dish { DishId = 5, PublicId = TestPublicId, RestaurantId = 1, DishName = "D", Slug = "d", Restaurant = restaurant };
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateDishAvailabilityCommand(TestPublicId, false), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_NOT_OWNER");
    }
}
