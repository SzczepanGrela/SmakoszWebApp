using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.RespondToContact;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.RespondToContact;

[Trait("Category", "Handlers")]
public class RespondToContactHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailService _email;
    private readonly IDateTimeProvider _dateTime;
    private readonly RespondToContactHandler _handler;

    public RespondToContactHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _email = Substitute.For<IEmailService>();
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _handler = new RespondToContactHandler(_db, _currentUser, _email, _dateTime);
    }

    [Fact]
    public async Task Handle_HappyPath_SendsEmailAndResolvesTicket()
    {
        var ticket = new SystemTicket
        {
            TicketId = 1, TicketType = TicketType.Contact, Status = TicketStatus.Open,
            Priority = 3, Description = "Od: Jan <jan@example.com>\nTemat: Test\n\nHello"
        };
        _sets.SystemTickets.Add(ticket);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RespondToContactCommand(1, "Thank you for contacting us."), CancellationToken.None);

        result.IsError.Should().BeFalse();
        ticket.Status.Should().Be(TicketStatus.Resolved);
        await _email.Received(1).SendContactResponseAsync(
            "jan@example.com", "Thank you for contacting us.", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NonContactTicket_ReturnsError()
    {
        var ticket = new SystemTicket
        {
            TicketId = 1, TicketType = TicketType.Photo, Status = TicketStatus.Open,
            Priority = 3, Description = "Photo ticket"
        };
        _sets.SystemTickets.Add(ticket);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RespondToContactCommand(1, "Response"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_NOT_CONTACT");
    }

    [Fact]
    public async Task Handle_AlreadyResolved_ReturnsError()
    {
        var ticket = new SystemTicket
        {
            TicketId = 1, TicketType = TicketType.Contact, Status = TicketStatus.Resolved,
            Priority = 3, Description = "Od: Jan <jan@example.com>\nTemat: Test\n\nHello"
        };
        _sets.SystemTickets.Add(ticket);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RespondToContactCommand(1, "Response"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_ALREADY_RESOLVED");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new RespondToContactHandler(_db, nonAdmin, _email, _dateTime);

        var result = await handler.Handle(
            new RespondToContactCommand(1, "Response"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
