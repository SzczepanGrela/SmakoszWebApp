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

        var targetCategories = new[]
        {
            TagCategories.DishCategory,
            TagCategories.Feature,
            TagCategories.Mood,
            TagCategories.Occasion,
            TagCategories.Spice
        };
        var allTags = await _db.Tags
            .AsNoTracking()
            .Where(t => targetCategories.Contains(t.Category))
            .OrderBy(t => t.TagName)
            .Select(t => new { t.Category, t.TagName })
            .ToListAsync(cancellationToken);

        List<FilterOption> ToOptions(string cat) => allTags
            .Where(t => t.Category == cat)
            .Select(t => new FilterOption { Value = t.TagName, Label = t.TagName })
            .ToList();

        var dishCategories = ToOptions(TagCategories.DishCategory);
        var features = ToOptions(TagCategories.Feature);
        var moods = ToOptions(TagCategories.Mood);
        var occasions = ToOptions(TagCategories.Occasion);
        var spiceLevels = ToOptions(TagCategories.Spice);

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

}
