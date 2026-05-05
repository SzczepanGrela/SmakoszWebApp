using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetUserRestaurantClaims;

public record GetUserRestaurantClaimsQuery(Guid PublicId, int Page = 1) : IRequest<ErrorOr<PagedResult<AdminUserRestaurantClaimDto>>>;

public class GetUserRestaurantClaimsHandler : IRequestHandler<GetUserRestaurantClaimsQuery, ErrorOr<PagedResult<AdminUserRestaurantClaimDto>>>
{
    private const int PageSize = 10;

    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUserRestaurantClaimsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminUserRestaurantClaimDto>>> Handle(GetUserRestaurantClaimsQuery request, CancellationToken cancellationToken)
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

        var query =
            from t in _db.SystemTickets.AsNoTracking()
            where t.RequesterId == user.UserId && t.TicketType == TicketType.RestaurantClaim
            join r in _db.Restaurants.AsNoTracking() on t.ReferenceId equals (long)r.RestaurantId into restaurants
            from r in restaurants.DefaultIfEmpty()
            orderby t.CreatedAt descending
            select new AdminUserRestaurantClaimDto
            {
                TicketId = t.TicketId,
                RestaurantId = (int)t.ReferenceId,
                RestaurantName = r != null ? r.RestaurantName : "(usunięta)",
                Status = t.Status.ToString(),
                CreatedAt = t.CreatedAt
            };

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.Page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);

        return new PagedResult<AdminUserRestaurantClaimDto>
        {
            Data = items,
            Pagination = new PaginationInfo
            {
                Page = request.Page,
                PageSize = PageSize,
                TotalCount = total,
                TotalPages = (int)Math.Ceiling(total / (double)PageSize)
            }
        };
    }
}
