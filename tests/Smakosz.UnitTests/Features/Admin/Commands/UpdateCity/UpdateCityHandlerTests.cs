using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.UpdateCity;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.UpdateCity;

[Trait("Category", "Handlers")]
public class UpdateCityHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateCityHandler _handler;

    public UpdateCityHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser();
        _handler = new UpdateCityHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesCityAndAudits()
    {
        _sets.Cities.Add(new City { CityId = 1, CityName = "Gdansk", Region = "Pomorskie" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateCityCommand(1, "Gdynia", null), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Cities[0].CityName.Should().Be("Gdynia");
        _sets.Cities[0].Region.Should().Be("Pomorskie");
        _sets.AuditLogs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new UpdateCityHandler(_db, nonAdmin);

        var result = await handler.Handle(new UpdateCityCommand(1, "X", null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new UpdateCityCommand(999, "X", null), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CITY_NOT_FOUND");
    }
}
