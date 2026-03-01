using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Admin.Queries.GetCities;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Queries.GetCities;

[Trait("Category", "Handlers")]
public class GetCitiesHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetCitiesHandler _handler;

    public GetCitiesHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new GetCitiesHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsPagedCities()
    {
        _sets.Cities.Add(new City { CityId = 1, CityName = "Gdansk" });
        _sets.Cities.Add(new City { CityId = 2, CityName = "Warszawa" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetCitiesQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_WithSearch_FiltersResults()
    {
        _sets.Cities.Add(new City { CityId = 1, CityName = "Gdansk" });
        _sets.Cities.Add(new City { CityId = 2, CityName = "Warszawa" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetCitiesQuery(new PaginationParams(1, 20), "gdansk"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new GetCitiesHandler(_db, nonAdmin);

        var result = await handler.Handle(
            new GetCitiesQuery(new PaginationParams(1, 20)), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}
