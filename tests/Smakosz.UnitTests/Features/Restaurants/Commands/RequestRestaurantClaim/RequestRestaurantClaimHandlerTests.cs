using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Restaurants.Commands.RequestRestaurantClaim;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Restaurants.Commands.RequestRestaurantClaim;

[Trait("Category", "Handlers")]
public class RequestRestaurantClaimHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _user;
    private readonly RequestRestaurantClaimHandler _handler;
    private readonly Guid _restaurantPublicId = Guid.NewGuid();

    public RequestRestaurantClaimHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _user = MockExtensions.CreateAuthenticatedUser(userId: 7, role: "User");
        _handler = new RequestRestaurantClaimHandler(_db, _user);
    }

    private void SeedOrphanRestaurant()
    {
        var restaurant = new RestaurantBuilder()
            .WithId(10)
            .WithPublicId(_restaurantPublicId)
            .WithName("Sultan Kebab")
            .Build();
        restaurant.OwnerId = null;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesOpenClaimTicket()
    {
        SeedOrphanRestaurant();

        var result = await _handler.Handle(
            new RequestRestaurantClaimCommand(_restaurantPublicId, "Jestem wlascicielem od 2018"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemTickets.Should().ContainSingle();
        var ticket = _sets.SystemTickets[0];
        ticket.TicketType.Should().Be(TicketType.RestaurantClaim);
        ticket.RequesterId.Should().Be(7);
        ticket.ReferenceId.Should().Be(10);
        ticket.Status.Should().Be(TicketStatus.Open);
        ticket.Description.Should().Be("Jestem wlascicielem od 2018");
    }

    [Fact]
    public async Task Handle_RestaurantNotFound_ReturnsNotFound()
    {
        var result = await _handler.Handle(
            new RequestRestaurantClaimCommand(Guid.NewGuid(), "uzasadnienie"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
        _sets.SystemTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RestaurantAlreadyClaimed_ReturnsConflict()
    {
        var restaurant = new RestaurantBuilder()
            .WithId(10)
            .WithPublicId(_restaurantPublicId)
            .Build();
        restaurant.OwnerId = 99;
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RequestRestaurantClaimCommand(_restaurantPublicId, "uzasadnienie"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_ALREADY_CLAIMED");
        _sets.SystemTickets.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PendingClaimByUser_ReturnsConflict()
    {
        SeedOrphanRestaurant();
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1,
            TicketType = TicketType.RestaurantClaim,
            RequesterId = 7,
            Status = TicketStatus.Open,
            ReferenceId = 99
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RequestRestaurantClaimCommand(_restaurantPublicId, "uzasadnienie"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CLAIM_ALREADY_PENDING");
        _sets.SystemTickets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_UserAlreadyOwnsRestaurant_ReturnsConflict()
    {
        SeedOrphanRestaurant();
        var owned = new RestaurantBuilder().WithId(20).WithName("Other").Build();
        owned.OwnerId = 7;
        _sets.Restaurants.Add(owned);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RequestRestaurantClaimCommand(_restaurantPublicId, "uzasadnienie"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_USER_ALREADY_OWNS_RESTAURANT");
        _sets.SystemTickets.Should().BeEmpty();
    }
}
