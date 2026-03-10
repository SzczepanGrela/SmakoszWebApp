using FluentAssertions;
using Smakosz.Application.Features.Dishes.Queries.GetDishBySlug;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Dishes.Queries;

[Trait("Category", "Handlers")]
public class GetDishBySlugHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetDishBySlugHandler _handler;

    public GetDishBySlugHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        _handler = new GetDishBySlugHandler(_db, anonymousUser);
    }

    [Fact]
    public async Task Handle_ExistingSlug_ReturnsDish()
    {
        var city = new City { CityId = 1, CityName = "Gdansk" };
        var restaurant = new RestaurantBuilder().WithCity(city).Build();
        var dish = new DishBuilder().WithSlug("margherita").WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetDishBySlugQuery("margherita"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Slug.Should().Be("margherita");
        result.Value.CityName.Should().Be("Gdansk");
    }

    [Fact]
    public async Task Handle_NonExistentSlug_ReturnsNotFound()
    {
        var result = await _handler.Handle(new GetDishBySlugQuery("nonexistent"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_AuthenticatedWithSaved_IsSavedTrue()
    {
        var authUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        var handler = new GetDishBySlugHandler(_db, authUser);
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().WithId(1).WithSlug("test-dish").WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        _sets.SavedDishes.Add(new SavedDish { UserId = 1, DishId = 1 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(new GetDishBySlugQuery("test-dish"), CancellationToken.None);

        result.Value.IsSaved.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AuthenticatedNotSaved_IsSavedFalse()
    {
        var authUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        var handler = new GetDishBySlugHandler(_db, authUser);
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().WithSlug("test-dish").WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(new GetDishBySlugQuery("test-dish"), CancellationToken.None);

        result.Value.IsSaved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Anonymous_IsSavedFalse()
    {
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().WithSlug("test-dish").WithRestaurant(restaurant).Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetDishBySlugQuery("test-dish"), CancellationToken.None);

        result.Value.IsSaved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PendingDish_ReturnsNotFound()
    {
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().WithSlug("pending-dish").WithRestaurant(restaurant).Build();
        dish.ModerationStatus = ContentModerationStatus.Pending;
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetDishBySlugQuery("pending-dish"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }
}
