using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetAdminRestaurants;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetAdminRestaurants;

[Trait("Category", "Handlers")]
public class GetAdminRestaurantsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetAdminRestaurantsHandler _handler;

    public GetAdminRestaurantsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetAdminRestaurantsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedRestaurants()
    {
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(1).WithName("Bella").Build());
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(2).WithName("Pizza Place").Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminRestaurantsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithSearch_FiltersResults()
    {
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(1).WithName("Bella Italia").Build());
        _sets.Restaurants.Add(new RestaurantBuilder().WithId(2).WithName("Pizza Place").Build());
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetAdminRestaurantsQuery(new PaginationParams(1, 20), "bella"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetAdminRestaurantsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetAdminRestaurantsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
