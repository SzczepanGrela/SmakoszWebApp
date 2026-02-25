using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Cities.Queries.GetPublicCities;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Cities.Queries.GetPublicCities;

[Trait("Category", "Handlers")]
public class GetPublicCitiesHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetPublicCitiesHandler _handler;

    public GetPublicCitiesHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetPublicCitiesHandler(_db);
    }

    [Fact]
    public async Task Handle_ReturnsAllCitiesOrderedByName()
    {
        _sets.Cities.Add(new City { CityId = 1, CityName = "Warszawa" });
        _sets.Cities.Add(new City { CityId = 2, CityName = "Krakow" });
        _sets.Cities.Add(new City { CityId = 3, CityName = "Gdansk" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetPublicCitiesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(3);
        result.Value[0].Name.Should().Be("Gdansk");
        result.Value[1].Name.Should().Be("Krakow");
        result.Value[2].Name.Should().Be("Warszawa");
    }

    [Fact]
    public async Task Handle_EmptyDb_ReturnsEmptyList()
    {
        var result = await _handler.Handle(new GetPublicCitiesQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }
}
