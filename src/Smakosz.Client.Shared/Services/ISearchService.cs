using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface ISearchService
{
    Task<SearchResultDto?> SearchAsync(string type = "restaurants", string? query = null, string? location = null,
        string? cuisines = null, int? minPrice = null, int? maxPrice = null, string? dietary = null,
        string sortBy = "rating", string sortDir = "desc", int page = 1, int pageSize = 20,
        double? lat = null, double? lng = null, double? radius = null, string? tags = null,
        string? dishCategories = null, string? features = null, string? moods = null,
        string? occasions = null, string? spiceLevels = null);
    Task<SearchFiltersDto?> GetFiltersAsync();
    Task<List<SuggestItemDto>?> SuggestAsync(string query, int limit = 7);
}
