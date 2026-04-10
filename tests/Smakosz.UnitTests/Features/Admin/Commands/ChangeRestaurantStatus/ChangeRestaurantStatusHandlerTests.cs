using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ChangeRestaurantStatus;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.ChangeRestaurantStatus;

[Trait("Category", "Handlers")]
public class ChangeRestaurantStatusHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ChangeRestaurantStatusHandler _handler;
    private static readonly Guid TestPublicId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public ChangeRestaurantStatusHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new ChangeRestaurantStatusHandler(_db, _currentUser);
    }

    private Restaurant CreateRestaurant(RestaurantStatus status = RestaurantStatus.Active)
    {
        var r = new Restaurant
        {
            RestaurantId = 1,
            PublicId = TestPublicId,
            RestaurantName = "Test",
            Slug = "test",
            Status = status,
            Version = 1
        };
        _sets.Restaurants.Add(r);
        DbContextMockFactory.Refresh(_db, _sets);
        return r;
    }

    [Theory]
    [InlineData(RestaurantStatus.Active, RestaurantStatus.Suspended)]
    [InlineData(RestaurantStatus.Active, RestaurantStatus.ClosedPermanently)]
    [InlineData(RestaurantStatus.Active, RestaurantStatus.Renovation)]
    [InlineData(RestaurantStatus.Suspended, RestaurantStatus.Active)]
    [InlineData(RestaurantStatus.PendingVerification, RestaurantStatus.Active)]
    [InlineData(RestaurantStatus.Renovation, RestaurantStatus.Active)]
    public async Task Handle_LegalTransition_Succeeds(RestaurantStatus from, RestaurantStatus to)
    {
        var r = CreateRestaurant(from);
        var reason = to is RestaurantStatus.Suspended or RestaurantStatus.ClosedPermanently ? "Test reason" : null;

        var result = await _handler.Handle(
            new ChangeRestaurantStatusCommand(TestPublicId, to, reason), CancellationToken.None);

        result.IsError.Should().BeFalse();
        r.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(RestaurantStatus.ClosedPermanently, RestaurantStatus.Active)]
    [InlineData(RestaurantStatus.ClosedPermanently, RestaurantStatus.Suspended)]
    [InlineData(RestaurantStatus.Renovation, RestaurantStatus.Suspended)]
    public async Task Handle_IllegalTransition_ReturnsError(RestaurantStatus from, RestaurantStatus to)
    {
        CreateRestaurant(from);

        var result = await _handler.Handle(
            new ChangeRestaurantStatusCommand(TestPublicId, to, "reason"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_INVALID_STATUS_TRANSITION");
    }

    [Fact]
    public async Task Handle_SameStatus_ReturnsError()
    {
        CreateRestaurant(RestaurantStatus.Active);

        var result = await _handler.Handle(
            new ChangeRestaurantStatusCommand(TestPublicId, RestaurantStatus.Active, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_SAME_STATUS");
    }

    [Fact]
    public async Task Handle_CreatesModerationLog()
    {
        CreateRestaurant();

        await _handler.Handle(
            new ChangeRestaurantStatusCommand(TestPublicId, RestaurantStatus.Suspended, "Spam"), CancellationToken.None);

        _sets.ModerationLogs.Should().ContainSingle();
        _sets.ModerationLogs[0].EntityType.Should().Be(ModerationEntityType.Restaurant);
        _sets.ModerationLogs[0].Verdict.Should().Be(ModerationVerdict.Rejected);
    }

    [Fact]
    public async Task Handle_CreatesAuditLog()
    {
        CreateRestaurant();

        await _handler.Handle(
            new ChangeRestaurantStatusCommand(TestPublicId, RestaurantStatus.Renovation, null), CancellationToken.None);

        _sets.AuditLogs.Should().ContainSingle();
        _sets.AuditLogs[0].TableName.Should().Be("restaurants");
    }

    [Fact]
    public async Task Handle_WithOwner_SendsNotification()
    {
        var r = CreateRestaurant();
        r.OwnerId = 10;
        DbContextMockFactory.Refresh(_db, _sets);

        await _handler.Handle(
            new ChangeRestaurantStatusCommand(TestPublicId, RestaurantStatus.Suspended, "Spam"), CancellationToken.None);

        _sets.Notifications.Should().ContainSingle();
        _sets.Notifications[0].UserId.Should().Be(10);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new ChangeRestaurantStatusHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new ChangeRestaurantStatusCommand(TestPublicId, RestaurantStatus.Active, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsNotFound()
    {
        var result = await _handler.Handle(
            new ChangeRestaurantStatusCommand(Guid.NewGuid(), RestaurantStatus.Active, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}
