using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetRegistrationStatus;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Queries.GetRegistrationStatus;

[Trait("Category", "Handlers")]
public class GetRegistrationStatusHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetRegistrationStatusHandler _handler;

    public GetRegistrationStatusHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new GetRegistrationStatusHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_WithRestaurant_ReturnsDetails()
    {
        var restaurant = new RestaurantBuilder().WithId(1).WithName("Bella").Build();
        restaurant.OwnerId = 1;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetRegistrationStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.HasRestaurant.Should().BeTrue();
        result.Value.RestaurantName.Should().Be("Bella");
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsDefaultResponse()
    {
        var result = await _handler.Handle(new GetRegistrationStatusQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.HasRestaurant.Should().BeFalse();
        result.Value.RestaurantName.Should().BeNull();
    }
}
