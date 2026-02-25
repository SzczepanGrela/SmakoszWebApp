using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetAdminIngredients;

public record GetAdminIngredientsQuery(PaginationParams Pagination, string? Search = null)
    : IRequest<ErrorOr<PagedResult<AdminIngredientDto>>>;

public class GetAdminIngredientsHandler : IRequestHandler<GetAdminIngredientsQuery, ErrorOr<PagedResult<AdminIngredientDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetAdminIngredientsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminIngredientDto>>> Handle(GetAdminIngredientsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.Ingredients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(i => i.IngredientName.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(i => i.IngredientName)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(i => new AdminIngredientDto
            {
                IngredientId = i.IngredientId,
                IngredientName = i.IngredientName,
                IsAllergen = i.IsAllergen,
                IsVegetarian = i.IsVegetarian,
                IsVegan = i.IsVegan,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminIngredientDto>
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
