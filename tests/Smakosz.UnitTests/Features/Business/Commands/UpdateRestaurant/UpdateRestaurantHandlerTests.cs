using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateRestaurant;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateRestaurant;

[Trait("Category", "Handlers")]
public class UpdateRestaurantHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateRestaurantHandler _handler;

    public UpdateRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _handler = new UpdateRestaurantHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ValidNameUpdate_UpdatesRestaurantName()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "Old Name", Slug = "old-name" };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateRestaurantCommand("New Name", null, null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        restaurant.RestaurantName.Should().Be("New Name");
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFound()
    {
        var result = await _handler.Handle(
            new UpdateRestaurantCommand("New Name", null, null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NullFields_OnlyUpdatesNonNullFields()
    {
        var restaurant = new Restaurant
        {
            RestaurantId = 1,
            OwnerId = 10,
            RestaurantName = "Original Name",
            Slug = "original-name",
            Description = "Original description",
            Address = "123 Original St",
            Phone = "+48000000000"
        };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateRestaurantCommand(null, null, null, "+48111111111", null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        restaurant.RestaurantName.Should().Be("Original Name");
        restaurant.Description.Should().Be("Original description");
        restaurant.Address.Should().Be("123 Original St");
        restaurant.Phone.Should().Be("+48111111111");
    }

    [Fact]
    public async Task Handle_AllFieldsProvided_UpdatesAllFields()
    {
        var restaurant = new Restaurant
        {
            RestaurantId = 1,
            OwnerId = 10,
            RestaurantName = "Old",
            Slug = "old",
            Description = "Old desc",
            Address = "Old address",
            Phone = "+48000000000",
            Email = "old@example.com",
            Website = "http://old.com",
            CityId = 1
        };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateRestaurantCommand("New", "New desc", "New address", "+48999999999", "new@example.com", "http://new.com", 2),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        restaurant.RestaurantName.Should().Be("New");
        restaurant.Description.Should().Be("New desc");
        restaurant.Address.Should().Be("New address");
        restaurant.Phone.Should().Be("+48999999999");
        restaurant.Email.Should().Be("new@example.com");
        restaurant.Website.Should().Be("http://new.com");
        restaurant.CityId.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsInvalidCredentials()
    {
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var handler = new UpdateRestaurantHandler(_db, anonymousUser);

        var result = await handler.Handle(
            new UpdateRestaurantCommand("New Name", null, null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
