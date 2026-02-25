using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetDashboard;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Queries.GetDashboard;

[Trait("Category", "Handlers")]
public class GetBusinessDashboardHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetBusinessDashboardHandler _handler;

    public GetBusinessDashboardHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _handler = new GetBusinessDashboardHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_OwnerHasRestaurant_ReturnsDashboard()
    {
        _sets.Restaurants.Add(new Restaurant
        {
            RestaurantId = 1,
            OwnerId = 10,
            RestaurantName = "TestRest",
            Slug = "testrest",
            AvgFoodScore = 4.5
        });
        _sets.Dishes.Add(new Dish { DishId = 1, RestaurantId = 1, DishName = "P1", Slug = "p1" });
        _sets.Dishes.Add(new Dish { DishId = 2, RestaurantId = 1, DishName = "P2", Slug = "p2" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessDashboardQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.RestaurantName.Should().Be("TestRest");
        result.Value.TotalDishes.Should().Be(2);
    }

    [Fact]
    public async Task Handle_OwnerNoRestaurant_ReturnsError()
    {
        var result = await _handler.Handle(new GetBusinessDashboardQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}
