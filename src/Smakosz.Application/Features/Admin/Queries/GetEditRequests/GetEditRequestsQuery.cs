using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetEditRequests;

public record GetEditRequestsQuery(PaginationParams Pagination)
    : IRequest<ErrorOr<PagedResult<EditRequestDto>>>;

public class GetEditRequestsHandler : IRequestHandler<GetEditRequestsQuery, ErrorOr<PagedResult<EditRequestDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetEditRequestsHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<EditRequestDto>>> Handle(GetEditRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.RestaurantEditRequests
            .AsNoTracking()
            .Where(r => r.Status == EditRequestStatus.Pending);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(r => new EditRequestDto
            {
                RequestId = r.RequestId,
                RestaurantName = r.Restaurant.RestaurantName,
                Username = r.User.Username,
                ChangeType = r.ChangeType.ToString(),
                Status = r.Status.ToString(),
                Payload = r.Payload,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<EditRequestDto>
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
