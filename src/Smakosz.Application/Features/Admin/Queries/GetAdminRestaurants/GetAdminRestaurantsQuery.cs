using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetAdminRestaurants;

public record GetAdminRestaurantsQuery(PaginationParams Pagination, string? Search = null, bool? IsOrphan = null)
    : IRequest<ErrorOr<PagedResult<AdminRestaurantDto>>>;

public class GetAdminRestaurantsHandler : IRequestHandler<GetAdminRestaurantsQuery, ErrorOr<PagedResult<AdminRestaurantDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminRestaurantsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminRestaurantDto>>> Handle(GetAdminRestaurantsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.Restaurants.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(r => r.RestaurantName.ToLower().Contains(search));
        }

        if (request.IsOrphan == true)
        {
            query = query.Where(r => r.OwnerId == null);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(r => new AdminRestaurantDto
            {
                RestaurantId = r.RestaurantId,
                PublicId = r.PublicId,
                Name = r.RestaurantName,
                Slug = r.Slug ?? string.Empty,
                Status = r.Status.ToString(),
                IsVerified = r.IsVerified,
                OwnerUsername = r.Owner != null ? r.Owner.Username : null,
                AverageRating = (decimal)(r.AvgFoodScore ?? 0),
                ReviewCount = _db.Reviews.Count(rv => rv.RestaurantId == r.RestaurantId && !rv.IsDeleted)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminRestaurantDto>
        {
            Data = items,
            Pagination = new PaginationInfo
            {
                Page = request.Pagination.Page,
                PageSize = request.Pagination.PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)request.Pagination.PageSize)
            }
        };
    }
}
