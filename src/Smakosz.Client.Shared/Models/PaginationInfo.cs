namespace Smakosz.Client.Models;

public class PaginationInfo
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class PagedResult<T>
{
    public List<T> Data { get; set; } = [];
    public PaginationInfo Pagination { get; set; } = new();
}
