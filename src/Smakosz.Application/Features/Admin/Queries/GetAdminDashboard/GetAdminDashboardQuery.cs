using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Dtos;

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
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var totalUsers = await _db.Users.CountAsync(u => !u.IsDeleted, cancellationToken);
        var totalRestaurants = await _db.Restaurants.CountAsync(cancellationToken);
        var totalReviews = await _db.Reviews.CountAsync(cancellationToken);
        var pendingReports = await _db.Reports
            .CountAsync(r => r.Status == Smakosz.Domain.Enums.ReportStatus.Pending, cancellationToken);
        var pendingCorrections = await _db.DataCorrectionRequests
            .CountAsync(c => c.Status == "pending", cancellationToken);

        return new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            TotalRestaurants = totalRestaurants,
            TotalReviews = totalReviews,
            PendingReports = pendingReports,
            PendingCorrections = pendingCorrections
        };
    }
}
