using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetTicketsSummary;

public class GetTicketsSummaryHandler : IRequestHandler<GetTicketsSummaryQuery, ErrorOr<List<TicketSummaryDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetTicketsSummaryHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<List<TicketSummaryDto>>> Handle(GetTicketsSummaryQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var raw = await _db.SystemTickets
            .AsNoTracking()
            .Where(t => t.Status == TicketStatus.Open)
            .GroupBy(t => t.TicketType)
            .Select(g => new { Key = g.Key, Count = g.Count(), Oldest = g.Min(t => t.CreatedAt) })
            .ToListAsync(cancellationToken);

        return raw.Select(r => new TicketSummaryDto(r.Key.ToString(), r.Count, r.Oldest)).ToList();
    }
}
