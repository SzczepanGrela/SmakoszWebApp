using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Search.Queries.GetSearchFilters;

public record GetSearchFiltersQuery() : IRequest<ErrorOr<SearchFiltersDto>>;

public class SearchFiltersDto
{
    public List<FilterOption> Cuisines { get; init; } = new();
    public List<string> Cities { get; init; } = new();
    public List<FilterOption> DietaryOptions { get; init; } = new();
    public List<FilterOption> DishCategories { get; init; } = new();
    public List<FilterOption> Features { get; init; } = new();
    public List<FilterOption> Moods { get; init; } = new();
    public List<FilterOption> Occasions { get; init; } = new();
    public List<FilterOption> SpiceLevels { get; init; } = new();
    public int MinPrice { get; init; }
    public int MaxPrice { get; init; }
}

public class FilterOption
{
    public string Value { get; init; } = default!;
    public string Label { get; init; } = default!;
}
