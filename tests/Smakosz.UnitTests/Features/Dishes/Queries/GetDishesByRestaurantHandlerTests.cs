using FluentAssertions;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Dishes.Queries.GetDishesByRestaurant;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Dishes.Queries;

[Trait("Category", "Handlers")]
public class GetDishesByRestaurantHandlerTests
{
    private readonly Smakosz.Application.Common.Interfaces.ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetDishesByRestaurantHandler _handler;
    private readonly PaginationParams _defaultPagination = new(Page: 1, PageSize: 10);

    public GetDishesByRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        _handler = new GetDishesByRestaurantHandler(_db, anonymousUser);
    }

    [Fact]
    public async Task Handle_ExistingRestaurant_ReturnsDishes()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("bella-italia").Build();
        var dish1 = new DishBuilder().WithId(1).WithName("Margherita").WithRestaurant(restaurant).Build();
        var dish2 = new DishBuilder().WithId(2).WithName("Carbonara").WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { dish1, dish2 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetDishesByRestaurantQuery("bella-italia", _defaultPagination), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new GetDishesByRestaurantQuery("nonexistent", _defaultPagination), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_OnlyAvailableDishes_Returned()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("test").Build();
        var available = new DishBuilder().WithId(1).WithRestaurant(restaurant).AsAvailable().Build();
        var unavailable = new DishBuilder().WithId(2).WithRestaurant(restaurant).AsUnavailable().Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { available, unavailable });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetDishesByRestaurantQuery("test", _defaultPagination), CancellationToken.None);

        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_AuthenticatedUser_SavedDishesMarked()
    {
        var authUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        var handler = new GetDishesByRestaurantHandler(_db, authUser);
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("test").Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        _sets.SavedDishes.Add(new SavedDish { UserId = 1, DishId = 1 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await handler.Handle(
            new GetDishesByRestaurantQuery("test", _defaultPagination), CancellationToken.None);

        result.Value.Data[0].IsSaved.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Anonymous_IsSavedFalse()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("test").Build();
        var dish = new DishBuilder().WithId(1).WithRestaurant(restaurant).Build();
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetDishesByRestaurantQuery("test", _defaultPagination), CancellationToken.None);

        result.Value.Data[0].IsSaved.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_PendingDish_NotReturned()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("test").Build();
        var approved = new DishBuilder().WithId(1).WithName("Approved").WithRestaurant(restaurant).Build();
        approved.ModerationStatus = ContentModerationStatus.Approved;
        var noneDish = new DishBuilder().WithId(2).WithName("None").WithRestaurant(restaurant).Build();
        noneDish.ModerationStatus = ContentModerationStatus.None;
        var pending = new DishBuilder().WithId(3).WithName("Pending").WithRestaurant(restaurant).Build();
        pending.ModerationStatus = ContentModerationStatus.Pending;
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { approved, noneDish, pending });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetDishesByRestaurantQuery("test", _defaultPagination), CancellationToken.None);

        result.Value.Data.Should().HaveCount(2);
        result.Value.Data.Select(d => d.DishName).Should().Contain("Approved").And.Contain("None");
    }

    [Fact]
    public async Task Handle_RejectedDish_NotReturned()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithSlug("test").Build();
        var approved = new DishBuilder().WithId(1).WithName("Approved").WithRestaurant(restaurant).Build();
        approved.ModerationStatus = ContentModerationStatus.Approved;
        var rejected = new DishBuilder().WithId(2).WithName("Rejected").WithRestaurant(restaurant).Build();
        rejected.ModerationStatus = ContentModerationStatus.Rejected;
        _sets.Restaurants.Add(restaurant);
        _sets.Dishes.AddRange(new[] { approved, rejected });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetDishesByRestaurantQuery("test", _defaultPagination), CancellationToken.None);

        result.Value.Data.Should().HaveCount(1);
        result.Value.Data[0].DishName.Should().Be("Approved");
    }
}
