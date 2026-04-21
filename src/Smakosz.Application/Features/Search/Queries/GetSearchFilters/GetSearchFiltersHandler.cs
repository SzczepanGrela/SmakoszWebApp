using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Constants;

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

        var dishCategories = await LoadTagOptionsAsync(TagCategories.DishCategory, cancellationToken);
        var features = await LoadTagOptionsAsync(TagCategories.Feature, cancellationToken);
        var moods = await LoadTagOptionsAsync(TagCategories.Mood, cancellationToken);
        var occasions = await LoadTagOptionsAsync(TagCategories.Occasion, cancellationToken);
        var spiceLevels = await LoadTagOptionsAsync(TagCategories.Spice, cancellationToken);

        return new SearchFiltersDto
        {
            Cuisines = cuisines,
            Cities = cities,
            DishCategories = dishCategories,
            DietaryOptions = new List<FilterOption>
            {
                new() { Value = "vegetarian", Label = "Wegetariańskie" },
                new() { Value = "vegan", Label = "Wegańskie" },
                new() { Value = "gluten_free", Label = "Bezglutenowe" },
                new() { Value = "lactose_free", Label = "Bezlaktozowe" }
            },
            Features = features,
            Moods = moods,
            Occasions = occasions,
            SpiceLevels = spiceLevels,
            MinPrice = 1,
            MaxPrice = 4
        };
    }

    private Task<List<FilterOption>> LoadTagOptionsAsync(string category, CancellationToken ct)
        => _db.Tags
            .AsNoTracking()
            .Where(t => t.Category == category)
            .OrderBy(t => t.TagName)
            .Select(t => new FilterOption { Value = t.TagName, Label = t.TagName })
            .ToListAsync(ct);
}
