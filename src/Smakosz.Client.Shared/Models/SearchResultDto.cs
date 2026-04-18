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
}

public class SearchFiltersDto
{
    public List<FilterOption> Cuisines { get; set; } = [];
    public List<FilterOption> DietaryOptions { get; set; } = [];
    public List<FilterOption> DishCategories { get; set; } = [];
    public List<string> Cities { get; set; } = [];
    public int MinPrice { get; set; }
    public int MaxPrice { get; set; }
}

public class FilterOption
{
    public string Value { get; set; } = default!;
    public string Label { get; set; } = default!;
}

public class SuggestItemDto
{
    public string Type { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Subtitle { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }
}
