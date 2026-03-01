using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Reports.Queries.GetReportReasons;

public record GetReportReasonsQuery : IRequest<ErrorOr<List<ReportReasonDto>>>;

public record ReportReasonDto(string ReasonCode, string LabelPl, string? Description);

public class GetReportReasonsHandler : IRequestHandler<GetReportReasonsQuery, ErrorOr<List<ReportReasonDto>>>
{
    private readonly ISmakoszDbContext _db;

    public GetReportReasonsHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<List<ReportReasonDto>>> Handle(GetReportReasonsQuery request, CancellationToken cancellationToken)
    {
        var reasons = await _db.ReportReasonDefinitions
            .AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.SeverityScore)
            .Select(r => new ReportReasonDto(r.ReasonCode, r.LabelPl, r.Description))
            .ToListAsync(cancellationToken);

        return reasons;
    }
}
