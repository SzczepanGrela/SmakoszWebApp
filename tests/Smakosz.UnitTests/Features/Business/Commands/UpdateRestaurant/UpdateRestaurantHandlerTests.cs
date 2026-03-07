using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateRestaurant;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateRestaurant;

[Trait("Category", "Handlers")]
public class UpdateRestaurantHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly UpdateRestaurantHandler _handler;

    public UpdateRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new UpdateRestaurantHandler(_db, _currentUser, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_TextChange_CreatesEditRequest()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "Old Name", Slug = "old-name" };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateRestaurantCommand("New Name", null, null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        restaurant.RestaurantName.Should().Be("Old Name", "text changes go through EditRequest, not applied directly");
        _sets.RestaurantEditRequests.Should().ContainSingle();
        _sets.RestaurantEditRequests[0].NewName.Should().Be("New Name");
        _sets.RestaurantEditRequests[0].ModerationStatus.Should().Be(ContentModerationStatus.Pending);
        _sets.SystemTickets.Should().ContainSingle();
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
    public async Task Handle_NonTextFieldsOnly_AppliesImmediately()
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
        _sets.RestaurantEditRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MixedFields_NonTextAppliedImmediately_TextCreatesEditRequest()
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

        restaurant.Address.Should().Be("New address");
        restaurant.Phone.Should().Be("+48999999999");
        restaurant.Email.Should().Be("new@example.com");
        restaurant.Website.Should().Be("http://new.com");
        restaurant.CityId.Should().Be(2);

        restaurant.RestaurantName.Should().Be("Old");
        restaurant.Description.Should().Be("Old desc");

        _sets.RestaurantEditRequests.Should().ContainSingle();
        _sets.RestaurantEditRequests[0].NewName.Should().Be("New");
        _sets.RestaurantEditRequests[0].NewDescription.Should().Be("New desc");
        _sets.RestaurantEditRequests[0].ModerationStatus.Should().Be(ContentModerationStatus.Pending);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsInvalidCredentials()
    {
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var handler = new UpdateRestaurantHandler(_db, anonymousUser, _forbiddenWords);

        var result = await handler.Handle(
            new UpdateRestaurantCommand("New Name", null, null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
