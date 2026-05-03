using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.ApproveRestaurantClaim;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.ApproveRestaurantClaim;

[Trait("Category", "Handlers")]
public class ApproveRestaurantClaimHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _admin;
    private readonly ApproveRestaurantClaimHandler _handler;

    public ApproveRestaurantClaimHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _admin = MockExtensions.CreateAdminUser(userId: 99);
        _handler = new ApproveRestaurantClaimHandler(_db, _admin);
    }

    private void SeedHappyPath(int restaurantId = 10, int requesterId = 7, int ticketId = 1, RestaurantStatus status = RestaurantStatus.PendingVerification)
    {
        var requester = new UserBuilder().WithId(requesterId).WithRole(UserRole.User).Build();
        var restaurant = new RestaurantBuilder().WithId(restaurantId).WithName("Sultan").WithStatus(status).Build();
        restaurant.OwnerId = null;
        restaurant.IsVerified = false;
        var ticket = new SystemTicket
        {
            TicketId = ticketId,
            TicketType = TicketType.RestaurantClaim,
            ReferenceId = restaurantId,
            RequesterId = requesterId,
            Status = TicketStatus.Open
        };
        _sets.Users.Add(requester);
        _sets.Restaurants.Add(restaurant);
        _sets.SystemTickets.Add(ticket);
        DbContextMockFactory.Refresh(_db, _sets);
    }

    [Fact]
    public async Task Handle_HappyPath_PromotesUserAndResolvesTicket()
    {
        SeedHappyPath();

        var result = await _handler.Handle(new ApproveRestaurantClaimCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var restaurant = _sets.Restaurants.Single();
        restaurant.OwnerId.Should().Be(7);
        restaurant.IsVerified.Should().BeTrue();
        restaurant.Status.Should().Be(RestaurantStatus.Active);
        _sets.Users.Single(u => u.UserId == 7).Role.Should().Be(UserRole.Restaurant);
        var ticket = _sets.SystemTickets.Single();
        ticket.Status.Should().Be(TicketStatus.Resolved);
        ticket.ResolvedAt.Should().NotBeNull();
        ticket.ResolvedByAdminId.Should().Be(99);
    }

    [Fact]
    public async Task Handle_NonAdminCaller_ReturnsForbidden()
    {
        SeedHappyPath();
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User");
        var handler = new ApproveRestaurantClaimHandler(_db, nonAdmin);

        var result = await handler.Handle(new ApproveRestaurantClaimCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
        _sets.Restaurants.Single().OwnerId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_TicketNotFound_ReturnsNotFound()
    {
        var result = await _handler.Handle(new ApproveRestaurantClaimCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_TicketWrongType_ReturnsValidationError()
    {
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1,
            TicketType = TicketType.RestaurantRequest,
            Status = TicketStatus.Open,
            RequesterId = 7
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ApproveRestaurantClaimCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_WRONG_TYPE");
    }

    [Fact]
    public async Task Handle_TicketNotOpen_ReturnsValidationError()
    {
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1,
            TicketType = TicketType.RestaurantClaim,
            Status = TicketStatus.Resolved,
            RequesterId = 7,
            ReferenceId = 10
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ApproveRestaurantClaimCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_NOT_PENDING");
    }

    [Fact]
    public async Task Handle_RestaurantAlreadyClaimedRace_ReturnsConflict()
    {
        var restaurant = new RestaurantBuilder().WithId(10).Build();
        restaurant.OwnerId = 50;
        var ticket = new SystemTicket
        {
            TicketId = 1,
            TicketType = TicketType.RestaurantClaim,
            ReferenceId = 10,
            RequesterId = 7,
            Status = TicketStatus.Open
        };
        _sets.Users.Add(new UserBuilder().WithId(7).Build());
        _sets.Restaurants.Add(restaurant);
        _sets.SystemTickets.Add(ticket);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new ApproveRestaurantClaimCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_ALREADY_CLAIMED");
    }

    [Fact]
    public async Task Handle_HappyPath_WritesAuditAndNotification()
    {
        SeedHappyPath();

        var result = await _handler.Handle(new ApproveRestaurantClaimCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.AuditLogs.Should().HaveCount(2);
        _sets.AuditLogs.Should().Contain(a => a.TableName == "restaurants");
        _sets.AuditLogs.Should().Contain(a => a.TableName == "users");
        _sets.Notifications.Should().ContainSingle();
        _sets.Notifications[0].UserId.Should().Be(7);
    }
}
