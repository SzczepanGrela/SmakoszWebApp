using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Queries.GetSavedDishes;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Queries.GetSavedDishes;

[Trait("Category", "Handlers")]
public class GetSavedDishesHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetSavedDishesHandler _handler;

    public GetSavedDishesHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new GetSavedDishesHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsPaginatedSavedDishesForCurrentUser()
    {
        var dish1 = new DishBuilder().WithId(10).WithName("Pizza").WithSlug("pizza").Build();
        var dish2 = new DishBuilder().WithId(11).WithName("Burger").WithSlug("burger").Build();

        _sets.Dishes.Add(dish1);
        _sets.Dishes.Add(dish2);
        _sets.SavedDishes.Add(new SavedDish { UserId = 1, DishId = 10, Dish = dish1, CreatedAt = DateTime.UtcNow });
        _sets.SavedDishes.Add(new SavedDish { UserId = 1, DishId = 11, Dish = dish2, CreatedAt = DateTime.UtcNow.AddMinutes(-5) });
        _sets.SavedDishes.Add(new SavedDish { UserId = 2, DishId = 10, Dish = dish1, CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetSavedDishesQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsInvalidCredentials()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetSavedDishesHandler(_db, anonymous);

        var result = await handler.Handle(
            new GetSavedDishesQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
