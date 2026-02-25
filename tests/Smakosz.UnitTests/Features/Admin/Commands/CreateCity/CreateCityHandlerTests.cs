using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.CreateCity;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.CreateCity;

[Trait("Category", "Handlers")]
public class CreateCityHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _dateTime;
    private readonly CreateCityHandler _handler;

    public CreateCityHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _dateTime = Substitute.For<IDateTimeProvider>();
        _dateTime.UtcNow.Returns(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        _handler = new CreateCityHandler(_db, _currentUser, _dateTime);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesCityAndReturnsId()
    {
        var result = await _handler.Handle(
            new CreateCityCommand("Warszawa", "00-"),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Cities.Should().HaveCount(1);
        _sets.Cities[0].CityName.Should().Be("Warszawa");
        _sets.Cities[0].Region.Should().Be("00-");
    }

    [Fact]
    public async Task Handle_DuplicateName_ReturnsAlreadyExistsError()
    {
        _sets.Cities.Add(new City { CityId = 1, CityName = "Warszawa" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new CreateCityCommand("warszawa", null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CITY_ALREADY_EXISTS");
    }
}
