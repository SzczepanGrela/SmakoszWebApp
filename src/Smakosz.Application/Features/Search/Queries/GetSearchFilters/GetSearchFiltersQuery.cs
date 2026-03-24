using ErrorOr;
using MediatR;

namespace Smakosz.Application.Features.Search.Queries.GetSearchFilters;

public record GetSearchFiltersQuery() : IRequest<ErrorOr<SearchFiltersDto>>;

public class SearchFiltersDto
{
    public List<string> Cuisines { get; init; } = new();
    public List<CityFilterDto> Cities { get; init; } = new();
    public List<string> DietaryOptions { get; init; } = new();
    public int MinPrice { get; init; }
    public int MaxPrice { get; init; }
}

public class CityFilterDto
{
    public int Id { get; init; }
    public string Name { get; init; } = default!;
}
