using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.VerifyRestaurant;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.VerifyRestaurant;

[Trait("Category", "Handlers")]
public class VerifyRestaurantHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly VerifyRestaurantHandler _handler;

    public VerifyRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new VerifyRestaurantHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_SetsVerifiedAndReturnsSuccess()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.IsVerified = false;
        restaurant.Status = RestaurantStatus.PendingVerification;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new VerifyRestaurantCommand(restaurant.PublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        restaurant.IsVerified.Should().BeTrue();
        restaurant.Status.Should().Be(RestaurantStatus.Active);
        _sets.ModerationLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new VerifyRestaurantHandler(_db, nonAdmin);

        var result = await handler.Handle(new VerifyRestaurantCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsError()
    {
        var result = await _handler.Handle(new VerifyRestaurantCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WithOwner_CreatesNotification()
    {
        var owner = new UserBuilder().WithId(5).Build();
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 5;
        restaurant.IsVerified = false;
        _sets.Users.Add(owner);
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new VerifyRestaurantCommand(restaurant.PublicId), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Notifications.Should().HaveCount(1);
        _sets.Notifications[0].UserId.Should().Be(5);
    }
}
