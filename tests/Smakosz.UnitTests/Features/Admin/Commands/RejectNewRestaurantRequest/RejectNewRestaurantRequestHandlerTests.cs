using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.RejectNewRestaurantRequest;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.RejectNewRestaurantRequest;

[Trait("Category", "Handlers")]
public class RejectNewRestaurantRequestHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _admin;
    private readonly RejectNewRestaurantRequestHandler _handler;

    public RejectNewRestaurantRequestHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _admin = MockExtensions.CreateAdminUser(userId: 99);
        _handler = new RejectNewRestaurantRequestHandler(_db, _admin);
    }

    private void SeedTicket(TicketType type = TicketType.RestaurantRequest, TicketStatus status = TicketStatus.Open)
    {
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1,
            TicketType = type,
            Status = status,
            RequesterId = 7
        });
        DbContextMockFactory.Refresh(_db, _sets);
    }

    [Fact]
    public async Task Handle_HappyPath_RejectsTicketAndNotifiesRequester()
    {
        SeedTicket();

        var result = await _handler.Handle(
            new RejectNewRestaurantRequestCommand(1, "Brak danych kontaktowych do weryfikacji"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        var ticket = _sets.SystemTickets.Single();
        ticket.Status.Should().Be(TicketStatus.Rejected);
        ticket.Resolution.Should().Be("Brak danych kontaktowych do weryfikacji");
        ticket.ResolvedByAdminId.Should().Be(99);
        _sets.AuditLogs.Should().ContainSingle(a => a.TableName == "system_tickets");
        _sets.Notifications.Should().ContainSingle(n => n.UserId == 7);
    }

    [Fact]
    public async Task Handle_NonAdminCaller_ReturnsForbidden()
    {
        SeedTicket();
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User");
        var handler = new RejectNewRestaurantRequestHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new RejectNewRestaurantRequestCommand(1, "Powod odrzucenia"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
        _sets.SystemTickets.Single().Status.Should().Be(TicketStatus.Open);
    }

    [Fact]
    public async Task Handle_TicketWrongType_ReturnsValidationError()
    {
        SeedTicket(type: TicketType.RestaurantClaim);

        var result = await _handler.Handle(
            new RejectNewRestaurantRequestCommand(1, "Powod odrzucenia"),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_WRONG_TYPE");
    }
}
