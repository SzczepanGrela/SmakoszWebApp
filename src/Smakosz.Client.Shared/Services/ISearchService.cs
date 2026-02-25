using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface ISearchService
{
    Task<SearchResultDto?> SearchAsync(string type = "restaurants", string? query = null, string? location = null,
        string? cuisines = null, int? minPrice = null, int? maxPrice = null, string? dietary = null,
        string sortBy = "rating", string sortDir = "desc", int page = 1, int pageSize = 20);
}
