using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Models;

namespace Smakosz.Application.Common.Extensions;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginationParams pagination,
        CancellationToken cancellationToken = default)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pagination.PageSize);

        var data = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Data = data,
            Pagination = new PaginationInfo
            {
                Page = pagination.Page,
                PageSize = pagination.PageSize,
                TotalPages = totalPages,
                TotalCount = totalCount
            }
        };
    }
}
