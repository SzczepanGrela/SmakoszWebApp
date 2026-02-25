using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.RegisterBusiness;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.RegisterBusiness;

[Trait("Category", "Handlers")]
public class RegisterBusinessHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly RegisterBusinessHandler _handler;

    public RegisterBusinessHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User", sessionId: 100);
        _handler = new RegisterBusinessHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesRestaurantWithPendingVerificationStatus()
    {
        var result = await _handler.Handle(
            new RegisterBusinessCommand(
                Name: "Kawiarnia Centralna",
                Description: "Cozy café",
                Address: "ul. Główna 1",
                Phone: "+48123456789",
                Email: "cafe@example.com",
                CityId: 1),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Restaurants.Should().HaveCount(1);
        _sets.Restaurants[0].RestaurantName.Should().Be("Kawiarnia Centralna");
        _sets.Restaurants[0].Status.Should().Be(RestaurantStatus.PendingVerification);
        _sets.Restaurants[0].OwnerId.Should().Be(5);
    }

    [Fact]
    public async Task Handle_AlreadyHasRestaurantPending_ReturnsRegistrationPendingError()
    {
        var existing = new RestaurantBuilder()
            .WithId(1)
            .WithStatus(RestaurantStatus.PendingVerification)
            .Build();
        existing.OwnerId = 5;
        _sets.Restaurants.Add(existing);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RegisterBusinessCommand("Another", null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_REGISTRATION_PENDING");
    }

    [Fact]
    public async Task Handle_AlreadyHasActiveRestaurant_ReturnsRestaurantExistsError()
    {
        var existing = new RestaurantBuilder()
            .WithId(1)
            .WithStatus(RestaurantStatus.Active)
            .Build();
        existing.OwnerId = 5;
        _sets.Restaurants.Add(existing);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new RegisterBusinessCommand("Another", null, null, null, null, null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("BUSINESS_RESTAURANT_EXISTS");
    }
}
