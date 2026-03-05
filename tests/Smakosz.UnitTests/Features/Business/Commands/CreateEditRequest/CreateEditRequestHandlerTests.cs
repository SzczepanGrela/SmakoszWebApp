using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.CreateEditRequest;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.CreateEditRequest;

[Trait("Category", "Handlers")]
public class CreateEditRequestHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly CreateEditRequestHandler _handler;

    public CreateEditRequestHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new CreateEditRequestHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesRequestAndTicket()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateEditRequestCommand("General", "{}", null, null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.RestaurantEditRequests.Should().HaveCount(1);
        _sets.SystemTickets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithTextChanges_SetsModerationStatusPending()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateEditRequestCommand("General", "{}", "New Name", "New Description", null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.RestaurantEditRequests.Should().ContainSingle();
        _sets.RestaurantEditRequests[0].ModerationStatus.Should().Be(ContentModerationStatus.Pending);
        _sets.SystemJobs.Should().BeEmpty("batch aggregator creates jobs, not individual handlers");
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var result = await _handler.Handle(
            new CreateEditRequestCommand("General", "{}", null, null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new CreateEditRequestHandler(_db, anonymous);

        var result = await handler.Handle(
            new CreateEditRequestCommand("General", "{}", null, null, null, null, null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
