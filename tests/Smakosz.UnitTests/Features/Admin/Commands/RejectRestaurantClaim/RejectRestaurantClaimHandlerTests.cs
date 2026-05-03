using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.RejectRestaurantClaim;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.RejectRestaurantClaim;

[Trait("Category", "Handlers")]
public class RejectRestaurantClaimHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _admin;
    private readonly RejectRestaurantClaimHandler _handler;

    public RejectRestaurantClaimHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _admin = MockExtensions.CreateAdminUser(userId: 99);
        _handler = new RejectRestaurantClaimHandler(_db, _admin);
    }

    private void SeedTicket(TicketType type = TicketType.RestaurantClaim, TicketStatus status = TicketStatus.Open)
    {
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1,
            TicketType = type,
            Status = status,
            RequesterId = 7,
            ReferenceId = 10
        });
        DbContextMockFactory.Refresh(_db, _sets);
    }

    [Fact]
    public async Task Handle_HappyPath_RejectsTicketAndNotifiesRequester()
    {
        SeedTicket();

        var result = await _handler.Handle(
            new RejectRestaurantClaimCommand(1, "Brak dokumentow potwierdzajacych"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var ticket = _sets.SystemTickets.Single();
        ticket.Status.Should().Be(TicketStatus.Rejected);
        ticket.Resolution.Should().Be("Brak dokumentow potwierdzajacych");
        ticket.ResolvedAt.Should().NotBeNull();
        ticket.ResolvedByAdminId.Should().Be(99);
        _sets.AuditLogs.Should().ContainSingle(a => a.TableName == "system_tickets");
        _sets.Notifications.Should().ContainSingle(n => n.UserId == 7);
    }

    [Fact]
    public async Task Handle_NonAdminCaller_ReturnsForbidden()
    {
        SeedTicket();
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User");
        var handler = new RejectRestaurantClaimHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new RejectRestaurantClaimCommand(1, "Powod odrzucenia"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
        _sets.SystemTickets.Single().Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task Handle_TicketWrongType_ReturnsValidationError()
    {
        SeedTicket(type: TicketType.RestaurantRequest);

        var result = await _handler.Handle(
            new RejectRestaurantClaimCommand(1, "Powod odrzucenia"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_WRONG_TYPE");
        _sets.SystemTickets.Single().Status.Should().Be(TicketStatus.Open);
    }
}
