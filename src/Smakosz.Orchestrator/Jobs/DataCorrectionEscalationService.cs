using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;

namespace Smakosz.Orchestrator.Jobs;

public class DataCorrectionEscalationService
{
    private const string EscalationPrefix = "[ESKALACJA]";

    private readonly ISmakoszDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<DataCorrectionEscalationService> _logger;

    public DataCorrectionEscalationService(
        ISmakoszDbContext db,
        IDateTimeProvider clock,
        ILogger<DataCorrectionEscalationService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task EscalateAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var overdueCorrections = await _db.DataCorrectionRequests
            .Where(c => c.Status == "pending" && c.ResponseDeadline != null && c.ResponseDeadline < now)
            .ToListAsync(ct);

        if (overdueCorrections.Count == 0)
        {
            return;
        }

        var correctionIds = overdueCorrections.Select(c => c.RequestId).ToList();

        var tickets = await _db.SystemTickets
            .Where(t => t.TicketType == TicketType.DataCorrection && correctionIds.Contains((int)t.ReferenceId))
            .ToListAsync(ct);

        var ticketsByRef = tickets.ToDictionary(t => (int)t.ReferenceId);

        foreach (var correction in overdueCorrections)
        {
            correction.Status = "escalated";
            correction.Version++;

            if (ticketsByRef.TryGetValue(correction.RequestId, out var ticket))
            {
                ticket.Priority = 5;
                if (ticket.Description is null || !ticket.Description.StartsWith(EscalationPrefix))
                {
                    ticket.Description = $"{EscalationPrefix} {ticket.Description ?? string.Empty}".Trim();
                }
                ticket.Version++;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "data-correction-escalation: escalated {Count} overdue data correction requests",
            overdueCorrections.Count);
    }
}
