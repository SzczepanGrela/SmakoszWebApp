using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Queries.GetFavoriteRestaurants;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Me.Queries.GetFavoriteRestaurants;

[Trait("Category", "Handlers")]
public class GetFavoriteRestaurantsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetFavoriteRestaurantsHandler _handler;

    public GetFavoriteRestaurantsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new GetFavoriteRestaurantsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsPaginatedFavoriteRestaurantsForCurrentUser()
    {
        var r1 = new RestaurantBuilder().WithId(10).WithName("Trattoria").WithSlug("trattoria").Build();
        var r2 = new RestaurantBuilder().WithId(11).WithName("Sushi Bar").WithSlug("sushi-bar").Build();
        _sets.Restaurants.Add(r1);
        _sets.Restaurants.Add(r2);

        _sets.FavoriteRestaurants.Add(new Domain.Entities.FavoriteRestaurant
        {
            UserId = 1, RestaurantId = 10, Restaurant = r1, CreatedAt = DateTime.UtcNow
        });
        _sets.FavoriteRestaurants.Add(new Domain.Entities.FavoriteRestaurant
        {
            UserId = 1, RestaurantId = 11, Restaurant = r2, CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        _sets.FavoriteRestaurants.Add(new Domain.Entities.FavoriteRestaurant
        {
            UserId = 2, RestaurantId = 10, Restaurant = r1, CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetFavoriteRestaurantsQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsInvalidCredentials()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetFavoriteRestaurantsHandler(_db, anonymous);

        var result = await handler.Handle(
            new GetFavoriteRestaurantsQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
