using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetCuisineTypes;

public record GetCuisineTypesQuery(PaginationParams Pagination, string? Search = null)
    : IRequest<ErrorOr<PagedResult<AdminCuisineTypeDto>>>;

public class GetCuisineTypesHandler : IRequestHandler<GetCuisineTypesQuery, ErrorOr<PagedResult<AdminCuisineTypeDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetCuisineTypesHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminCuisineTypeDto>>> Handle(GetCuisineTypesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.CuisineTypes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(search) || c.DisplayName.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(c => c.DisplayName)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(c => new AdminCuisineTypeDto
            {
                Id = c.CuisineTypeId,
                Name = c.Name,
                DisplayName = c.DisplayName,
                Icon = c.Icon,
                IsActive = c.IsActive,
                RestaurantCount = _db.Restaurants.Count(r => r.CuisineTypeId == c.CuisineTypeId)
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminCuisineTypeDto>
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
