using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Search.Queries.GetSearchFilters;

public class GetSearchFiltersHandler : IRequestHandler<GetSearchFiltersQuery, ErrorOr<SearchFiltersDto>>
{
    private readonly ISmakoszDbContext _db;

    public GetSearchFiltersHandler(ISmakoszDbContext db) => _db = db;

    public async Task<ErrorOr<SearchFiltersDto>> Handle(GetSearchFiltersQuery request, CancellationToken cancellationToken)
    {
        var cuisines = await _db.CuisineTypes
            .AsNoTracking()
            .OrderBy(c => c.DisplayName)
            .Select(c => new FilterOption { Value = c.Name, Label = c.DisplayName })
            .ToListAsync(cancellationToken);

        var cities = await _db.Cities
            .AsNoTracking()
            .OrderBy(c => c.CityName)
            .Select(c => c.CityName)
            .ToListAsync(cancellationToken);

        return new SearchFiltersDto
        {
            Cuisines = cuisines,
            Cities = cities,
            DietaryOptions = new List<FilterOption>
            {
                new() { Value = "vegetarian", Label = "Wegetariańskie" },
                new() { Value = "vegan", Label = "Wegańskie" },
                new() { Value = "gluten_free", Label = "Bezglutenowe" },
                new() { Value = "lactose_free", Label = "Bezlaktozowe" }
            },
            MinPrice = 1,
            MaxPrice = 4
        };
    }
}
