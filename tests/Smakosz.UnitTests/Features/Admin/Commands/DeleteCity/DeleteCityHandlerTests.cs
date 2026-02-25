using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.DeleteCity;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Admin.Commands.DeleteCity;

[Trait("Category", "Handlers")]
public class DeleteCityHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly DeleteCityHandler _handler;

    public DeleteCityHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAdminUser(userId: 99, sessionId: 100);
        _handler = new DeleteCityHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_DeletesCityAndReturnsSuccess()
    {
        _sets.Cities.Add(new City { CityId = 1, CityName = "Testowo" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteCityCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CityNotFound_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(new DeleteCityCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CITY_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_CityHasRestaurants_ReturnsValidationError()
    {
        _sets.Cities.Add(new City { CityId = 1, CityName = "Testowo" });
        _sets.Restaurants.Add(new Restaurant { RestaurantId = 1, CityId = 1, RestaurantName = "R", Slug = "r" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteCityCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("CITY_HAS_RESTAURANTS");
    }

    [Fact]
    public async Task Handle_NonAdmin_ReturnsForbidden()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User");
        var handler = new DeleteCityHandler(_db, nonAdmin);

        var result = await handler.Handle(new DeleteCityCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
    }
}

[Trait("Category", "Validators")]
public class DeleteCityValidatorTests
{
    private readonly DeleteCityValidator _validator = new();

    [Fact]
    public void Validate_ValidCityId_NoErrors()
    {
        var result = _validator.Validate(new DeleteCityCommand(1));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidCityId_ReturnsError(int cityId)
    {
        var result = _validator.Validate(new DeleteCityCommand(cityId));
        result.IsValid.Should().BeFalse();
    }
}
