using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.SaveDish;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.SaveDish;

[Trait("Category", "Handlers")]
public class SaveDishHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly SaveDishHandler _handler;

    public SaveDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new SaveDishHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidDish_SavesSuccessfully()
    {
        _sets.Dishes.Add(new Dish { DishId = 1, Slug = "pierogi", DishName = "Pierogi" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new SaveDishCommand("pierogi"), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AlreadySaved_ReturnsError()
    {
        _sets.Dishes.Add(new Dish { DishId = 1, Slug = "pierogi", DishName = "Pierogi" });
        _sets.SavedDishes.Add(new Domain.Entities.SavedDish { UserId = 1, DishId = 1, CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new SaveDishCommand("pierogi"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("SAVED_DISH_ALREADY_SAVED");
    }

    [Fact]
    public async Task Handle_DishNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new SaveDishCommand("nonexistent"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }
}
