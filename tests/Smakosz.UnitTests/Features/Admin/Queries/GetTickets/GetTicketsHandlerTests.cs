using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetTickets;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetTickets;

[Trait("Category", "Handlers")]
public class GetTicketsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetTicketsHandler _handler;

    public GetTicketsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetTicketsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedTickets()
    {
        _sets.SystemTickets.Add(new SystemTicket { TicketId = 1, TicketType = TicketType.Contact, Status = TicketStatus.Open, Priority = 3, CreatedAt = DateTime.UtcNow });
        _sets.SystemTickets.Add(new SystemTicket { TicketId = 2, TicketType = TicketType.Photo, Status = TicketStatus.Resolved, Priority = 2, CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetTicketsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_FiltersResults()
    {
        _sets.SystemTickets.Add(new SystemTicket { TicketId = 1, TicketType = TicketType.Contact, Status = TicketStatus.Open, Priority = 3, CreatedAt = DateTime.UtcNow });
        _sets.SystemTickets.Add(new SystemTicket { TicketId = 2, TicketType = TicketType.Photo, Status = TicketStatus.Resolved, Priority = 2, CreatedAt = DateTime.UtcNow });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetTicketsQuery(new PaginationParams(1, 20), Status: "Open"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetTicketsHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetTicketsQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
