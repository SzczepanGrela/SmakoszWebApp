using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.AdminDeleteDish;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.AdminDeleteDish;

[Trait("Category", "Handlers")]
public class AdminDeleteDishHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly AdminDeleteDishHandler _handler;

    public AdminDeleteDishHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new AdminDeleteDishHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_DeletesDishAndWritesAuditLog_WhenAdmin()
    {
        var dish = new Dish
        {
            DishId = 42,
            PublicId = Guid.NewGuid(),
            DishName = "Pizza",
            RestaurantId = 1,
            Price = 25m,
            IsAvailable = true
        };
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new AdminDeleteDishCommand(dish.PublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Dishes.Should().NotContain(dish);
        var audit = _sets.AuditLogs.Should().ContainSingle().Which;
        audit.TableName.Should().Be("dishes");
        audit.RecordId.Should().Be(42);
        audit.Operation.Should().Be(AuditOperation.Delete);
        audit.OldValues.Should().Contain("Pizza");
    }

    [Fact]
    public async Task Handle_DishNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new AdminDeleteDishCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new AdminDeleteDishHandler(_db, nonAdmin);

        var result = await handler.Handle(new AdminDeleteDishCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
