using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetOpeningHours;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Queries.GetOpeningHours;

[Trait("Category", "Handlers")]
public class GetOpeningHoursHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetOpeningHoursHandler _handler;

    public GetOpeningHoursHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "Business", sessionId: 100);
        _handler = new GetOpeningHoursHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsOpeningHoursForOwnersRestaurant()
    {
        var restaurant = new RestaurantBuilder().WithId(10).Build();
        restaurant.OwnerId = 5;
        _sets.Restaurants.Add(restaurant);
        _sets.OpeningHours.Add(new RestaurantOpeningHours
        {
            HoursId = 1,
            RestaurantId = 10,
            DayOfWeek = 1,
            OpenTime = new TimeOnly(8, 0),
            CloseTime = new TimeOnly(22, 0),
            IsClosed = false
        });
        _sets.OpeningHours.Add(new RestaurantOpeningHours
        {
            HoursId = 2,
            RestaurantId = 10,
            DayOfWeek = 0,
            OpenTime = new TimeOnly(0, 0),
            CloseTime = new TimeOnly(0, 0),
            IsClosed = true
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetOpeningHoursQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(h => h.DayOfWeek == 1 && !h.IsClosed);
        result.Value.Should().Contain(h => h.DayOfWeek == 0 && h.IsClosed);
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(new GetOpeningHoursQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}
