namespace Smakosz.Client.Models;

public class SearchResultDto
{
    public string Type { get; set; } = default!;
    public List<RestaurantCardDto> Restaurants { get; set; } = [];
    public List<DishCardDto> Dishes { get; set; } = [];
    public PaginationInfo Pagination { get; set; } = new();
    public AppliedFiltersDto AppliedFilters { get; set; } = new();
}

public class AppliedFiltersDto
{
    public string Type { get; set; } = default!;
    public List<string> Cuisines { get; set; } = [];
    public List<string> Dietary { get; set; } = [];
    public bool GeoEnabled { get; set; }
}

public class SearchFiltersDto
{
    public List<string> Cuisines { get; set; } = [];
    public List<string> DietaryOptions { get; set; } = [];
    public List<string> Cities { get; set; } = [];
    public int MinPrice { get; set; }
    public int MaxPrice { get; set; }
}
