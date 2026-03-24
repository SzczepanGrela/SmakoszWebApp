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
            .Select(c => c.Name)
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);

        var cities = await _db.Cities
            .AsNoTracking()
            .Select(c => new CityFilterDto { Id = c.CityId, Name = c.CityName })
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        return new SearchFiltersDto
        {
            Cuisines = cuisines,
            Cities = cities,
            DietaryOptions = new List<string> { "vegetarian", "vegan", "gluten_free", "lactose_free" },
            MinPrice = 1,
            MaxPrice = 4
        };
    }
}
