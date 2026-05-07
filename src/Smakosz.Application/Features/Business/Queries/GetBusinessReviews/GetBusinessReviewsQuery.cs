using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Business.Dtos;

namespace Smakosz.Application.Features.Business.Queries.GetBusinessReviews;

public record GetBusinessReviewsQuery(PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<BusinessReviewDto>>>;

public class GetBusinessReviewsHandler : IRequestHandler<GetBusinessReviewsQuery, ErrorOr<PagedResult<BusinessReviewDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidationConfigProvider _config;

    public GetBusinessReviewsHandler(ISmakoszDbContext db, ICurrentUserService currentUser, IValidationConfigProvider config)
    {
        _db = db;
        _currentUser = currentUser;
        _config = config;
    }

    public async Task<ErrorOr<PagedResult<BusinessReviewDto>>> Handle(
        GetBusinessReviewsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var restaurant = await _db.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.OwnerId == _currentUser.UserId.Value, cancellationToken);

        if (restaurant is null)
            return DomainErrors.Restaurant.NotFound;

        var query = _db.Reviews
            .AsNoTracking()
            .Where(r => r.RestaurantId == restaurant.RestaurantId && !r.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);

        var defaultPageSize = _config.GetInt("business.default_page_size", 20);
        var maxPageSize = _config.GetInt("business.max_page_size", 100);
        var page = Math.Max(1, request.Pagination.Page);
        var pageSize = Math.Clamp(request.Pagination.PageSize > 0 ? request.Pagination.PageSize : defaultPageSize, 1, maxPageSize);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new BusinessReviewDto
            {
                ReviewId = r.ReviewId,
                Username = r.User.Username,
                DishName = r.Dish.DishName,
                DishRating = r.DishRating,
                ServiceRating = r.ServiceRating,
                Content = r.Content,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<BusinessReviewDto>
        {
            Data = reviews,
            Pagination = new PaginationInfo
            {
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount
            }
        };
    }
}
