using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Application.Features.Restaurants.Dtos;

namespace Smakosz.Application.Features.Search.Dtos;

public class SearchResultDto
{
    public string Type { get; init; } = default!;
    public required List<RestaurantCardDto> Restaurants { get; init; }
    public required List<DishCardDto> Dishes { get; init; }
    public required PaginationInfo Pagination { get; init; }
    public required AppliedFiltersDto AppliedFilters { get; init; }
}

public class AppliedFiltersDto
{
    public string Type { get; init; } = default!;
    public List<string> Cuisines { get; init; } = [];
    public List<string> Dietary { get; init; } = [];
}
