using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;

namespace Smakosz.Application.Features.Admin.Queries.GetUserReviews;

public record GetUserReviewsQuery(Guid PublicId, int Page = 1) : IRequest<ErrorOr<PagedResult<AdminUserReviewDto>>>;

public class GetUserReviewsHandler : IRequestHandler<GetUserReviewsQuery, ErrorOr<PagedResult<AdminUserReviewDto>>>
{
    private const int PageSize = 10;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserReviewsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminUserReviewDto>>> Handle(GetUserReviewsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.PublicId == request.PublicId && !u.IsDeleted)
            .Select(u => new { u.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return DomainErrors.User.NotFound;

        var query = _db.Reviews
            .AsNoTracking()
            .Where(r => r.UserId == user.UserId && !r.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Page - 1) * PageSize)
            .Take(PageSize)
            .Select(r => new AdminUserReviewDto
            {
                PublicId = r.PublicId,
                DishPublicId = r.Dish.PublicId,
                DishName = r.Dish.DishName,
                RestaurantName = r.Restaurant.RestaurantName,
                DishRating = r.DishRating,
                ModerationStatus = r.ModerationStatus.ToString(),
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminUserReviewDto>
        {
            Data = items,
            Pagination = new PaginationInfo
            {
                Page = request.Page,
                PageSize = PageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)PageSize)
            }
        };
    }
}
