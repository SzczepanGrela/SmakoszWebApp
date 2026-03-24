using System.Web;
using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class SearchService : ISearchService
{
    private readonly SmakoszApiClient _api;

    public SearchService(SmakoszApiClient api) => _api = api;

    public Task<SearchResultDto?> SearchAsync(string type = "restaurants", string? query = null, string? location = null,
        string? cuisines = null, int? minPrice = null, int? maxPrice = null, string? dietary = null,
        string sortBy = "rating", string sortDir = "desc", int page = 1, int pageSize = 20)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["type"] = type;
        qs["page"] = page.ToString();
        qs["pageSize"] = pageSize.ToString();
        qs["sortBy"] = sortBy;
        qs["sortDir"] = sortDir;
        if (!string.IsNullOrWhiteSpace(query)) qs["query"] = query;
        if (!string.IsNullOrWhiteSpace(location)) qs["location"] = location;
        if (!string.IsNullOrWhiteSpace(cuisines)) qs["cuisines"] = cuisines;
        if (minPrice.HasValue) qs["minPrice"] = minPrice.Value.ToString();
        if (maxPrice.HasValue) qs["maxPrice"] = maxPrice.Value.ToString();
        if (!string.IsNullOrWhiteSpace(dietary)) qs["dietary"] = dietary;

        return _api.GetAsync<SearchResultDto>($"/api/search?{qs}");
    }
}
