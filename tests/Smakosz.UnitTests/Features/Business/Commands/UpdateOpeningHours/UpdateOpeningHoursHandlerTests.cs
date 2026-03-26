using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateOpeningHours;
using Smakosz.Application.Features.Business.Dtos;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateOpeningHours;

[Trait("Category", "Handlers")]
public class UpdateOpeningHoursHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateOpeningHoursHandler _handler;

    public UpdateOpeningHoursHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new UpdateOpeningHoursHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReplacesHours()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var hours = new List<OpeningHoursItemDto>
        {
            new() { DayOfWeek = 0, OpenTime = "08:00", CloseTime = "20:00", IsClosed = false },
            new() { DayOfWeek = 1, OpenTime = "08:00", CloseTime = "20:00", IsClosed = false }
        };

        var result = await _handler.Handle(new UpdateOpeningHoursCommand(hours), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.OpeningHours.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsError()
    {
        var result = await _handler.Handle(
            new UpdateOpeningHoursCommand(new List<OpeningHoursItemDto>()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new UpdateOpeningHoursHandler(_db, anonymous);

        var result = await handler.Handle(
            new UpdateOpeningHoursCommand(new List<OpeningHoursItemDto>()), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
