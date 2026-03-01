using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetBusinessStats;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Queries.GetBusinessStats;

[Trait("Category", "Handlers")]
public class GetBusinessStatsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetBusinessStatsHandler _handler;

    public GetBusinessStatsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new GetBusinessStatsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsStats()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        _sets.Restaurants.Add(restaurant);
        _sets.Reviews.Add(new ReviewBuilder().WithId(1).WithRestaurantId(1).Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessStatsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TotalReviews.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsError()
    {
        var result = await _handler.Handle(new GetBusinessStatsQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}
