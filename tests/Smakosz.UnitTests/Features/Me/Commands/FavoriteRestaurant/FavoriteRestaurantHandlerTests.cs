using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.FavoriteRestaurant;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.FavoriteRestaurant;

[Trait("Category", "Handlers")]
public class FavoriteRestaurantHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly FavoriteRestaurantHandler _handler;

    public FavoriteRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new FavoriteRestaurantHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_AddsFavoriteAndReturnsSuccess()
    {
        var restaurant = new RestaurantBuilder().WithId(10).WithSlug("test-restaurant").Build();
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new FavoriteRestaurantCommand("test-restaurant"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.FavoriteRestaurants.Should().HaveCount(1);
        _sets.FavoriteRestaurants[0].UserId.Should().Be(1);
        _sets.FavoriteRestaurants[0].RestaurantId.Should().Be(10);
    }

    [Fact]
    public async Task Handle_AlreadyFavorited_ReturnsAlreadyFavoritedError()
    {
        var restaurant = new RestaurantBuilder().WithId(10).WithSlug("test-restaurant").Build();
        _sets.Restaurants.Add(restaurant);
        _sets.FavoriteRestaurants.Add(new Domain.Entities.FavoriteRestaurant
        {
            UserId = 1,
            RestaurantId = 10
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new FavoriteRestaurantCommand("test-restaurant"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FAVORITE_RESTAURANT_ALREADY_FAVORITED");
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(
            new FavoriteRestaurantCommand("nonexistent-slug"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}
