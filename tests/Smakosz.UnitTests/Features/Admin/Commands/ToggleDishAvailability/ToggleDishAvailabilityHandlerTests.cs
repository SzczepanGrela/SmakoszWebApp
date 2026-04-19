using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ToggleDishAvailability;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.ToggleDishAvailability;

[Trait("Category", "Handlers")]
public class ToggleDishAvailabilityHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ToggleDishAvailabilityHandler _handler;

    public ToggleDishAvailabilityHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new ToggleDishAvailabilityHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_UpdatesAvailabilityAndWritesAuditLog()
    {
        var dish = new Dish
        {
            DishId = 77,
            PublicId = Guid.NewGuid(),
            DishName = "Soup",
            IsAvailable = true
        };
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ToggleDishAvailabilityCommand(dish.PublicId, false), CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.IsAvailable.Should().BeFalse();
        var audit = _sets.AuditLogs.Should().ContainSingle().Which;
        audit.TableName.Should().Be("dishes");
        audit.RecordId.Should().Be(77);
        audit.Operation.Should().Be(AuditOperation.Update);
        audit.NewValues.Should().Contain("false");
    }

    [Fact]
    public async Task Handle_DishNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new ToggleDishAvailabilityCommand(Guid.NewGuid(), true), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ToggleDishAvailabilityHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new ToggleDishAvailabilityCommand(Guid.NewGuid(), false), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
