using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetMyRestaurant;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Queries.GetMyRestaurant;

[Trait("Category", "Handlers")]
public class GetMyRestaurantHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetMyRestaurantHandler _handler;

    public GetMyRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "Business", sessionId: 100);
        _handler = new GetMyRestaurantHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsOwnersRestaurant()
    {
        var restaurant = new RestaurantBuilder()
            .WithId(10)
            .WithName("My Restaurant")
            .WithSlug("my-restaurant")
            .Build();
        restaurant.OwnerId = 5;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetMyRestaurantQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Name.Should().Be("My Restaurant");
        result.Value.Slug.Should().Be("my-restaurant");
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(new GetMyRestaurantQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}
