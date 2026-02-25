using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.UnfavoriteRestaurant;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Commands.UnfavoriteRestaurant;

[Trait("Category", "Handlers")]
public class UnfavoriteRestaurantHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UnfavoriteRestaurantHandler _handler;

    public UnfavoriteRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new UnfavoriteRestaurantHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_RemovesFavoriteAndReturnsSuccess()
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
            new UnfavoriteRestaurantCommand("test-restaurant"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.FavoriteRestaurants.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotFavorited_ReturnsNotFavoritedError()
    {
        var restaurant = new RestaurantBuilder().WithId(10).WithSlug("test-restaurant").Build();
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UnfavoriteRestaurantCommand("test-restaurant"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FAVORITE_RESTAURANT_NOT_FAVORITED");
    }
}
