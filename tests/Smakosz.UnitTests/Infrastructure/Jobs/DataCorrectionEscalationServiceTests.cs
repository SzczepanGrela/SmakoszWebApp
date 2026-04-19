using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Orchestrator.Jobs;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Infrastructure.Jobs;

[Trait("Category", "Handlers")]
public class DataCorrectionEscalationServiceTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly IDateTimeProvider _clock;
    private readonly DataCorrectionEscalationService _service;

    private static readonly DateTime Now = new(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc);

    public DataCorrectionEscalationServiceTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(Now);
        var logger = Substitute.For<ILogger<DataCorrectionEscalationService>>();
        _service = new DataCorrectionEscalationService(_db, _clock, logger);
    }

    private static DataCorrectionRequest CreateCorrection(int id, DateTime? deadline, string status = "pending") => new()
    {
        RequestId = id,
        RestaurantId = 100 + id,
        IssueType = DataCorrectionIssueType.Address,
        Status = status,
        CreatedAt = Now.AddDays(-8),
        ResponseDeadline = deadline,
        Version = 1
    };

    private static SystemTicket CreateTicket(int ticketId, int correctionId, int priority = 3, string? description = "Korekta danych") => new()
    {
        TicketId = ticketId,
        TicketType = TicketType.DataCorrection,
        ReferenceId = correctionId,
        Status = TicketStatus.Open,
        Priority = priority,
        Description = description,
        Version = 1
    };

    [Fact]
    public async Task EscalateAsync_NoOverdueCorrections_DoesNothing()
    {
        _sets.DataCorrectionRequests.Add(CreateCorrection(1, Now.AddDays(1)));
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.EscalateAsync(CancellationToken.None);

        _sets.DataCorrectionRequests.Single().Status.Should().Be("pending");
    }

    [Fact]
    public async Task EscalateAsync_OverdueCorrection_UpdatesStatusAndTicketPriority()
    {
        var correction = CreateCorrection(42, Now.AddHours(-1));
        var ticket = CreateTicket(500, 42, priority: 3, description: "Korekta danych restauracji");
        _sets.DataCorrectionRequests.Add(correction);
        _sets.SystemTickets.Add(ticket);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.EscalateAsync(CancellationToken.None);

        correction.Status.Should().Be("escalated");
        correction.Version.Should().Be(2);
        ticket.Priority.Should().Be(5);
        ticket.Description.Should().StartWith("[ESKALACJA]");
        ticket.Description.Should().Contain("Korekta danych restauracji");
        ticket.Version.Should().Be(2);
    }

    [Fact]
    public async Task EscalateAsync_AlreadyEscalated_IsSkipped()
    {
        var correction = CreateCorrection(42, Now.AddHours(-1), status: "escalated");
        var ticket = CreateTicket(500, 42, priority: 5, description: "[ESKALACJA] Korekta");
        _sets.DataCorrectionRequests.Add(correction);
        _sets.SystemTickets.Add(ticket);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.EscalateAsync(CancellationToken.None);

        correction.Version.Should().Be(1);
        ticket.Priority.Should().Be(5);
        ticket.Version.Should().Be(1);
    }

    [Fact]
    public async Task EscalateAsync_TicketDescriptionAlreadyPrefixed_DoesNotDoublePrefix()
    {
        var correction = CreateCorrection(42, Now.AddHours(-1));
        var ticket = CreateTicket(500, 42, priority: 3, description: "[ESKALACJA] Korekta");
        _sets.DataCorrectionRequests.Add(correction);
        _sets.SystemTickets.Add(ticket);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.EscalateAsync(CancellationToken.None);

        ticket.Description.Should().Be("[ESKALACJA] Korekta");
        ticket.Priority.Should().Be(5);
    }

    [Fact]
    public async Task EscalateAsync_MultipleOverdueCorrections_EscalatesAll()
    {
        var c1 = CreateCorrection(1, Now.AddHours(-2));
        var c2 = CreateCorrection(2, Now.AddDays(-1));
        var c3 = CreateCorrection(3, Now.AddDays(1));
        var t1 = CreateTicket(501, 1);
        var t2 = CreateTicket(502, 2);
        var t3 = CreateTicket(503, 3);
        _sets.DataCorrectionRequests.Add(c1);
        _sets.DataCorrectionRequests.Add(c2);
        _sets.DataCorrectionRequests.Add(c3);
        _sets.SystemTickets.Add(t1);
        _sets.SystemTickets.Add(t2);
        _sets.SystemTickets.Add(t3);
        DbContextMockFactory.Refresh(_db, _sets);

        await _service.EscalateAsync(CancellationToken.None);

        c1.Status.Should().Be("escalated");
        c2.Status.Should().Be("escalated");
        c3.Status.Should().Be("pending");
        t1.Priority.Should().Be(5);
        t2.Priority.Should().Be(5);
        t3.Priority.Should().Be(3);
    }
}
