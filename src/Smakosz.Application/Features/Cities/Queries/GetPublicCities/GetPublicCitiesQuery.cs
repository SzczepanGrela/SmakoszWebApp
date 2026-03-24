using ErrorOr;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Features.Cities.Queries.GetPublicCities;

public record GetPublicCitiesQuery : IRequest<ErrorOr<List<PublicCityDto>>>;

public class PublicCityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GetPublicCitiesHandler : IRequestHandler<GetPublicCitiesQuery, ErrorOr<List<PublicCityDto>>>
{
    private readonly ISmakoszDbContext _db;

    public GetPublicCitiesHandler(ISmakoszDbContext db)
    {
        _db = db;
    }

    public async Task<ErrorOr<List<PublicCityDto>>> Handle(GetPublicCitiesQuery request, CancellationToken cancellationToken)
    {
        var cities = await _db.Cities
            .AsNoTracking()
            .OrderBy(c => c.CityName)
            .Select(c => new PublicCityDto
            {
                Id = c.CityId,
                Name = c.CityName
            })
            .ToListAsync(cancellationToken);

        return cities;
    }
}
