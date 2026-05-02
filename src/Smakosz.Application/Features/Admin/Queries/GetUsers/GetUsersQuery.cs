using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Dtos;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Admin.Queries.GetUsers;

public record GetUsersQuery(PaginationParams Pagination, string? Search = null, UserRole? Role = null) : IRequest<ErrorOr<PagedResult<AdminUserDto>>>;

public class GetUsersHandler : IRequestHandler<GetUsersQuery, ErrorOr<PagedResult<AdminUserDto>>>
{
    private readonly ISmakoszDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public GetUsersHandler(ISmakoszDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ErrorOr<PagedResult<AdminUserDto>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return DomainErrors.Admin.Forbidden;

        var query = _db.Users.AsNoTracking().Where(u => !u.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(search) || u.Email.ToLower().Contains(search));
        }

        if (request.Role.HasValue)
        {
            var roleFilter = request.Role.Value;
            query = query.Where(u => u.Role == roleFilter);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((request.Pagination.Page - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(u => new AdminUserDto
            {
                UserId = u.UserId,
                PublicId = u.PublicId,
                Username = u.Username,
                Email = u.Email,
                Role = u.Role.ToString(),
                Status = u.IsBanned ? "Banned" : u.IsActive ? "Active" : "Inactive",
                EmailVerified = u.EmailVerified,
                Is2faEnabled = u.Is2faEnabled,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminUserDto>
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
