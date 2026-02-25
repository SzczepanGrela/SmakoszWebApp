using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Search.Queries.GetSearchFilters;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Search.Queries.GetSearchFilters;

[Trait("Category", "Handlers")]
public class GetSearchFiltersHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly GetSearchFiltersHandler _handler;

    public GetSearchFiltersHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _handler = new GetSearchFiltersHandler(_db);
    }

    [Fact]
    public async Task Handle_ReturnsFilters_WithCuisinesAndCities()
    {
        _sets.CuisineTypes.Add(new CuisineType { CuisineTypeId = 1, Name = "Polska", DisplayName = "Kuchnia Polska" });
        _sets.CuisineTypes.Add(new CuisineType { CuisineTypeId = 2, Name = "Włoska", DisplayName = "Kuchnia Włoska" });
        _sets.Cities.Add(new City { CityId = 1, CityName = "Rzeszów" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetSearchFiltersQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Cuisines.Should().HaveCount(2);
        result.Value.Cities.Should().HaveCount(1);
        result.Value.DietaryOptions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_EmptyDb_ReturnsEmptyLists()
    {
        var result = await _handler.Handle(new GetSearchFiltersQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Cuisines.Should().BeEmpty();
        result.Value.Cities.Should().BeEmpty();
    }
}
