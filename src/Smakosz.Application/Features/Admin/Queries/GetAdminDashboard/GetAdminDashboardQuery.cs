using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetAdminDashboard;

public record GetAdminDashboardQuery() : IRequest<ErrorOr<AdminDashboardDto>>;

public class GetAdminDashboardHandler : IRequestHandler<GetAdminDashboardQuery, ErrorOr<AdminDashboardDto>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminDashboardHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<AdminDashboardDto>> Handle(GetAdminDashboardQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdminOrModerator)
            return DomainErrors.Admin.Forbidden;

        var cache = await _db.HomePageCaches.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        var pendingReports = await _db.Reports
            .CountAsync(r => r.Status == ReportStatus.Pending, cancellationToken);
        var pendingCorrections = await _db.DataCorrectionRequests
            .CountAsync(c => c.Status == "pending", cancellationToken);
        var pendingPhotos = await _db.MediaAssets
            .CountAsync(m => m.ModerationStatus == ContentModerationStatus.Pending
                          || m.ModerationStatus == ContentModerationStatus.NeedsReview, cancellationToken);
        var pendingReviews = await _db.Reviews
            .CountAsync(r => (r.ModerationStatus == ContentModerationStatus.Pending
                           || r.ModerationStatus == ContentModerationStatus.NeedsReview) && !r.IsDeleted, cancellationToken);
        var openTickets = await _db.SystemTickets
            .CountAsync(t => t.Status == TicketStatus.Open, cancellationToken);

        return new AdminDashboardDto
        {
            TotalUsers = cache?.TotalUsers ?? 0,
            TotalRestaurants = cache?.TotalRestaurants ?? 0,
            TotalReviews = cache?.TotalReviews ?? 0,
            PendingReports = pendingReports,
            PendingCorrections = pendingCorrections,
            PendingPhotos = pendingPhotos,
            PendingReviews = pendingReviews,
            OpenTickets = openTickets
        };
    }
}
