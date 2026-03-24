using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Dtos;

namespace Smakosz.Application.Features.Me.Queries.GetMyReviews;

public record GetMyReviewsQuery(PaginationParams Pagination) : IRequest<ErrorOr<PagedResult<MyReviewDto>>>;

public class GetMyReviewsHandler : IRequestHandler<GetMyReviewsQuery, ErrorOr<PagedResult<MyReviewDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetMyReviewsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<MyReviewDto>>> Handle(GetMyReviewsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
            return DomainErrors.Auth.InvalidCredentials;

        var userId = _currentUser.UserId.Value;

        var query = _db.Reviews
            .AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(r => new MyReviewDto
            {
                ReviewId = r.ReviewId,
                DishName = r.Dish.DishName,
                DishSlug = r.Dish.Slug,
                RestaurantName = r.Restaurant.RestaurantName,
                RestaurantSlug = r.Restaurant.Slug,
                DishRating = r.DishRating,
                Content = r.Content,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<MyReviewDto>
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
