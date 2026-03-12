using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Reports.Commands.CreateReport;

public record CreateReportCommand(
    Guid ReviewPublicId,
    List<string> ReasonCodes,
    string? Description) : IRequest<ErrorOr<Success>>;

public class CreateReportHandler : IRequestHandler<CreateReportCommand, ErrorOr<Success>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateReportHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<Success>> Handle(CreateReportCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        if (request.ReasonCodes is not { Count: > 0 })
            return DomainErrors.Report.InvalidReasonCode;

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.PublicId == request.ReviewPublicId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return DomainErrors.Review.NotFound;

        var alreadyReported = await _db.Reports
            .AnyAsync(r => r.ReporterId == _currentUser.UserId.Value
                && r.EntityType == ReportEntityType.Review
                && r.EntityId == review.ReviewId, cancellationToken);

        if (alreadyReported)
            return Error.Conflict("REPORT_ALREADY_EXISTS", "Juz zglosiles te recenzje");

        var validReasons = await _db.ReportReasonDefinitions
            .Where(r => r.IsActive && request.ReasonCodes.Contains(r.ReasonCode))
            .ToListAsync(cancellationToken);

        if (validReasons.Count != request.ReasonCodes.Count)
            return DomainErrors.Report.InvalidReasonCode;

        var maxSeverity = validReasons.Max(r => r.SeverityScore);
        var reasonLabels = string.Join(", ", validReasons.Select(r => r.LabelPl));

        var report = new Report
        {
            ReporterId = _currentUser.UserId.Value,
            EntityType = ReportEntityType.Review,
            EntityId = review.ReviewId,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? reasonLabels
                : $"{reasonLabels}: {request.Description}",
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        foreach (var code in request.ReasonCodes)
        {
            _db.ReportReasonAssignments.Add(new ReportReasonAssignment
            {
                ReportId = report.ReportId,
                ReasonCode = code
            });
        }

        var ticketPriority = maxSeverity switch
        {
            >= 4 => 1,
            3 => 2,
            _ => 3
        };

        _db.SystemTickets.Add(new SystemTicket
        {
            TicketType = TicketType.Report,
            ReferenceId = report.ReportId,
            Status = TicketStatus.Open,
            Priority = ticketPriority,
            Description = $"Zgłoszenie recenzji #{review.ReviewId}: {reasonLabels}"
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
