using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetIngredientSuggestions;

public record GetIngredientSuggestionsQuery(PaginationParams Pagination, string? Status = null)
    : IRequest<ErrorOr<PagedResult<IngredientSuggestionDto>>>;

public class GetIngredientSuggestionsHandler
    : IRequestHandler<GetIngredientSuggestionsQuery, ErrorOr<PagedResult<IngredientSuggestionDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetIngredientSuggestionsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<IngredientSuggestionDto>>> Handle(
        GetIngredientSuggestionsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.IngredientSuggestions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Restaurant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<Smakosz.Domain.Enums.IngredientSuggestionStatus>(request.Status, true, out var statusEnum))
        {
            query = query.Where(s => s.Status == statusEnum);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(s => new IngredientSuggestionDto
            {
                SuggestionId = s.SuggestionId,
                SuggestedName = s.SuggestedName,
                IsAllergen = s.IsAllergen,
                IsVegetarian = s.IsVegetarian,
                IsVegan = s.IsVegan,
                IsGlutenFree = s.IsGlutenFree,
                IsLactoseFree = s.IsLactoseFree,
                Status = s.Status.ToString(),
                AdminNote = s.AdminNote,
                Username = s.User != null ? s.User.Username : null,
                RestaurantName = s.Restaurant.RestaurantName,
                CreatedAt = s.CreatedAt,
                ReviewedAt = s.ReviewedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<IngredientSuggestionDto>
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
