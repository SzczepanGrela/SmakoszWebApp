namespace Smakosz.Application.Common.Models;

public class PagedResult<T>
{
    public required List<T> Data { get; init; }
    public required PaginationInfo Pagination { get; init; }
}

public class PaginationInfo
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
}

public record PaginationParams(int Page = 1, int PageSize = 20);
