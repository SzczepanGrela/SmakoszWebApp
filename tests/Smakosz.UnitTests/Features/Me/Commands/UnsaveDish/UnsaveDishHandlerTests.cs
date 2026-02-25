using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.UnsaveDish;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.UnsaveDish;

[Trait("Category", "Handlers")]
public class UnsaveDishHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UnsaveDishHandler _handler;

    public UnsaveDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new UnsaveDishHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_RemovesSavedDishAndReturnsSuccess()
    {
        var dish = new DishBuilder().WithId(10).WithSlug("test-dish").Build();
        _sets.Dishes.Add(dish);
        _sets.SavedDishes.Add(new SavedDish { UserId = 1, DishId = 10 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UnsaveDishCommand("test-dish"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SavedDishes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotSaved_ReturnsNotSavedError()
    {
        var dish = new DishBuilder().WithId(10).WithSlug("test-dish").Build();
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UnsaveDishCommand("test-dish"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("SAVED_DISH_NOT_SAVED");
    }
}
