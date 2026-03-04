using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetPendingReviews;

public record GetPendingReviewsQuery(PaginationParams Pagination)
    : IRequest<ErrorOr<PagedResult<ReviewModerationDto>>>;

public class GetPendingReviewsHandler : IRequestHandler<GetPendingReviewsQuery, ErrorOr<PagedResult<ReviewModerationDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetPendingReviewsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<ReviewModerationDto>>> Handle(GetPendingReviewsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is not "Admin" and not "Moderator")
            return DomainErrors.Admin.Forbidden;

        var query = _db.Reviews
            .AsNoTracking()
            .Where(r => r.ContentStatus == ReviewContentStatus.Pending && !r.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(r => new ReviewModerationDto
            {
                ReviewId = r.ReviewId,
                PublicId = r.PublicId,
                Username = r.User.Username,
                DishName = r.Dish.DishName,
                RestaurantName = r.Restaurant.RestaurantName,
                Content = r.Content,
                DishRating = r.DishRating,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ReviewModerationDto>
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
