using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ChangeDishModerationStatus;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.ChangeDishModerationStatus;

[Trait("Category", "Handlers")]
public class ChangeDishModerationStatusHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ChangeDishModerationStatusHandler _handler;

    public ChangeDishModerationStatusHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new ChangeDishModerationStatusHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ApprovesDish_WritesAuditAndModerationLogs()
    {
        var dish = new Dish
        {
            DishId = 10,
            PublicId = Guid.NewGuid(),
            DishName = "Burger",
            ModerationStatus = ContentModerationStatus.Pending
        };
        _sets.Dishes.Add(dish);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ChangeDishModerationStatusCommand(dish.PublicId, "Approved"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        dish.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        _sets.AuditLogs.Should().ContainSingle(a => a.Operation == AuditOperation.Update && a.RecordId == 10);
        var modLog = _sets.ModerationLogs.Should().ContainSingle().Which;
        modLog.EntityType.Should().Be(ModerationEntityType.Dish);
        modLog.EntityId.Should().Be(10);
        modLog.Actor.Should().Be(ModerationActor.Admin);
        modLog.Verdict.Should().Be(ModerationVerdict.Approved);
    }

    [Fact]
    public async Task Handle_InvalidStatus_ReturnsValidationError()
    {
        var result = await _handler.Handle(
            new ChangeDishModerationStatusCommand(Guid.NewGuid(), "NotAStatus"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_INVALID_MODERATION_STATUS");
    }

    [Fact]
    public async Task Handle_DishNotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new ChangeDishModerationStatusCommand(Guid.NewGuid(), "Rejected"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("DISH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ChangeDishModerationStatusHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new ChangeDishModerationStatusCommand(Guid.NewGuid(), "Approved"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
