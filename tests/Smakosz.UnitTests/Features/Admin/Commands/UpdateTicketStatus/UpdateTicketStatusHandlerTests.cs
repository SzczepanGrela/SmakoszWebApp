using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateTicketStatus;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateTicketStatus;

[Trait("Category", "Handlers")]
public class UpdateTicketStatusHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateTicketStatusHandler _handler;

    public UpdateTicketStatusHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new UpdateTicketStatusHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesStatus()
    {
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1, TicketType = TicketType.Contact, Status = TicketStatus.Open, Priority = 3
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateTicketStatusCommand(1, "Resolved"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.SystemTickets[0].Status.Should().Be(TicketStatus.Resolved);
    }

    [Fact]
    public async Task Handle_InvalidStatus_ReturnsError()
    {
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1, TicketType = TicketType.Contact, Status = TicketStatus.Open, Priority = 3
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateTicketStatusCommand(1, "InvalidXYZ"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_INVALID_STATUS");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new UpdateTicketStatusHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new UpdateTicketStatusCommand(1, "Resolved"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(
            new UpdateTicketStatusCommand(999, "Resolved"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_NOT_FOUND");
    }
}
