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
    string Reason,
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

        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.PublicId == request.ReviewPublicId && !r.IsDeleted, cancellationToken);

        if (review is null)
            return DomainErrors.Review.NotFound;

        var alreadyReported = await _db.Reports
            .AnyAsync(r => r.ReporterId == _currentUser.UserId.Value
                && r.EntityType == ReportEntityType.Review
                && r.EntityId == review.ReviewId, cancellationToken);

        if (alreadyReported)
            return Error.Conflict("REPORT_ALREADY_EXISTS", "Już zgłosiłeś te recenzje");

        var report = new Report
        {
            ReporterId = _currentUser.UserId.Value,
            EntityType = ReportEntityType.Review,
            EntityId = review.ReviewId,
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? request.Reason
                : $"{request.Reason}: {request.Description}",
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Reports.Add(report);
        await _db.SaveChangesAsync(cancellationToken);

        _db.SystemTickets.Add(new SystemTicket
        {
            TicketType = TicketType.Report,
            ReferenceId = report.ReportId,
            Status = TicketStatus.Open,
            Priority = 2,
            Description = $"Zgloszenie recenzji #{review.ReviewId}: {request.Reason}"
        });

        await _db.SaveChangesAsync(cancellationToken);

        return Result.Success;
    }
}
