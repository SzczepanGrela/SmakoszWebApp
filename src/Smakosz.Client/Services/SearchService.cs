using System.Globalization;
using System.Web;
using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class SearchService : ISearchService
{
    private readonly SmakoszApiClient _api;

    public SearchService(SmakoszApiClient api) => _api = api;

    public Task<SearchResultDto?> SearchAsync(string type = "restaurants", string? query = null, string? location = null,
        string? cuisines = null, int? minPrice = null, int? maxPrice = null, string? dietary = null,
        string sortBy = "rating", string sortDir = "desc", int page = 1, int pageSize = 20,
        double? lat = null, double? lng = null, double? radius = null, string? tags = null,
        string? dishCategories = null)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["type"] = type;
        qs["page"] = page.ToString();
        qs["pageSize"] = pageSize.ToString();
        qs["sortBy"] = sortBy;
        qs["sortDir"] = sortDir;
        if (!string.IsNullOrWhiteSpace(query)) qs["q"] = query;
        if (!string.IsNullOrWhiteSpace(location)) qs["location"] = location;
        if (!string.IsNullOrWhiteSpace(cuisines)) qs["cuisines"] = cuisines;
        if (minPrice.HasValue) qs["minPrice"] = minPrice.Value.ToString();
        if (maxPrice.HasValue) qs["maxPrice"] = maxPrice.Value.ToString();
        if (!string.IsNullOrWhiteSpace(dietary)) qs["dietary"] = dietary;
        if (lat.HasValue) qs["lat"] = lat.Value.ToString(CultureInfo.InvariantCulture);
        if (lng.HasValue) qs["lng"] = lng.Value.ToString(CultureInfo.InvariantCulture);
        if (radius.HasValue) qs["radius"] = radius.Value.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(tags)) qs["tags"] = tags;
        if (!string.IsNullOrWhiteSpace(dishCategories)) qs["dishCategories"] = dishCategories;

        return _api.GetAsync<SearchResultDto>($"/api/search?{qs}");
    }

    public Task<SearchFiltersDto?> GetFiltersAsync()
        => _api.GetAsync<SearchFiltersDto>("/api/search/filters");

    public Task<List<SuggestItemDto>?> SuggestAsync(string query, int limit = 7)
        => _api.GetAsync<List<SuggestItemDto>>($"/api/search/suggest?q={Uri.EscapeDataString(query)}&limit={limit}");
}
