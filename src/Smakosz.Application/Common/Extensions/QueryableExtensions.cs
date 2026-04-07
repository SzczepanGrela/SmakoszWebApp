using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Models;
using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Application.Common.Extensions;

public static class QueryableExtensions
{
    public static IQueryable<T> WhereModerated<T>(this IQueryable<T> query) where T : class, IModerable
        => query.Where(e => e.ModerationStatus == ContentModerationStatus.None
                         || e.ModerationStatus == ContentModerationStatus.Approved);

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

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PaginationParams pagination,
        int maxPageSize,
        CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(pagination.PageSize, 1, maxPageSize);
        var clamped = new PaginationParams(Math.Max(1, pagination.Page), pageSize);
        return await query.ToPagedResultAsync(clamped, cancellationToken);
    }
}
