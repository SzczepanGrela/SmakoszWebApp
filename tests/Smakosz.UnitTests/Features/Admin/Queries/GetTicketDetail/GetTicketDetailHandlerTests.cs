using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Queries.GetTicketDetail;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetTicketDetail;

[Trait("Category", "Handlers")]
public class GetTicketDetailHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetTicketDetailHandler _handler;

    public GetTicketDetailHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetTicketDetailHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsTicketWithRelatedData()
    {
        _sets.SystemTickets.Add(new SystemTicket
        {
            TicketId = 1, TicketType = TicketType.Contact, Status = TicketStatus.Open,
            Priority = 3, Description = "Od: Jan <jan@ex.com>\nTemat: Help\n\nBody",
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetTicketDetailQuery(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.TicketId.Should().Be(1);
        result.Value.TicketType.Should().Be("Contact");
        result.Value.Contact.Should().NotBeNull();
        result.Value.Contact!.Email.Should().Be("jan@ex.com");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetTicketDetailHandler(_db, nonAdmin);

        var result = await handler.Handle(new GetTicketDetailQuery(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new GetTicketDetailQuery(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("TICKET_NOT_FOUND");
    }
}
