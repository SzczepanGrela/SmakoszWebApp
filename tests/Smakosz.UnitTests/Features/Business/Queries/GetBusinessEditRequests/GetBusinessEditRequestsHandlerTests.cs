using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetBusinessEditRequests;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Queries.GetBusinessEditRequests;

[Trait("Category", "Handlers")]
public class GetBusinessEditRequestsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetBusinessEditRequestsHandler _handler;

    public GetBusinessEditRequestsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _handler = new GetBusinessEditRequestsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsEditRequests()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "R", Slug = "r" };
        _sets.Restaurants.Add(restaurant);
        _sets.RestaurantEditRequests.Add(new RestaurantEditRequest
        {
            RequestId = 1,
            RestaurantId = 1,
            UserId = 10,
            ChangeType = EditRequestChangeType.General,
            Status = EditRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Restaurant = restaurant
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessEditRequestsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFound()
    {
        var result = await _handler.Handle(new GetBusinessEditRequestsQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_EmptyEditRequests_ReturnsEmptyList()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "R", Slug = "r" };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetBusinessEditRequestsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
