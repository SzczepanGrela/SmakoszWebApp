using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Admin.Commands.AdminCreateRestaurant;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Admin.Commands.AdminCreateRestaurant;

[Trait("Category", "Handlers")]
public class AdminCreateRestaurantHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _admin;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly AdminCreateRestaurantHandler _handler;

    public AdminCreateRestaurantHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _admin = MockExtensions.CreateAdminUser(userId: 99);
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new AdminCreateRestaurantHandler(_db, _admin, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_HappyPathNoOwner_CreatesActiveVerifiedRestaurant()
    {
        var result = await _handler.Handle(
            new AdminCreateRestaurantCommand(
                Name: "Kawiarnia Centralna",
                Address: "ul. Glowna 1",
                CityId: 1,
                CuisineTypeId: 2,
                Phone: "+48123456789",
                Email: "cafe@example.com",
                Description: "Cozy cafe",
                OwnerId: null,
                TicketId: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Restaurants.Should().HaveCount(1);
        var created = _sets.Restaurants[0];
        created.RestaurantName.Should().Be("Kawiarnia Centralna");
        created.OwnerId.Should().BeNull();
        created.Status.Should().Be(RestaurantStatus.Active);
        created.IsVerified.Should().BeTrue();
        created.ModerationStatus.Should().Be(ContentModerationStatus.Approved);
        _sets.AuditLogs.Should().ContainSingle(l => l.TableName == "restaurants");
    }

    [Fact]
    public async Task Handle_HappyPathWithOwner_PromotesUserToRestaurantRole()
    {
        var owner = new UserBuilder().WithId(42).WithRole(UserRole.User).Build();
        _sets.Users.Add(owner);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new AdminCreateRestaurantCommand(
                Name: "Pizzeria Roma",
                Address: "ul. Marszalkowska 10",
                CityId: 1,
                CuisineTypeId: 3,
                Phone: null,
                Email: null,
                Description: null,
                OwnerId: 42,
                TicketId: null),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Restaurants.Should().ContainSingle();
        _sets.Restaurants[0].OwnerId.Should().Be(42);
        _sets.Users.Single(u => u.UserId == 42).Role.Should().Be(UserRole.Restaurant);
    }

    [Fact]
    public async Task Handle_NonAdminCaller_ReturnsForbiddenAndWritesNothing()
    {
        var nonAdmin = MockExtensions.CreateAuthenticatedUser(userId: 5, role: "User");
        var handler = new AdminCreateRestaurantHandler(_db, nonAdmin, _forbiddenWords);

        var result = await handler.Handle(
            new AdminCreateRestaurantCommand(
                Name: "Bistro",
                Address: "ul. Testowa 1",
                CityId: 1,
                CuisineTypeId: 1,
                Phone: null,
                Email: null,
                Description: null,
                OwnerId: null,
                TicketId: null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("ADMIN_FORBIDDEN");
        _sets.Restaurants.Should().BeEmpty();
        _sets.AuditLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ForbiddenWordInName_ReturnsErrorAndWritesNothing()
    {
        _forbiddenWords.ContainsAsync("Bad Name", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(true);

        var result = await _handler.Handle(
            new AdminCreateRestaurantCommand(
                Name: "Bad Name",
                Address: "ul. Testowa 1",
                CityId: 1,
                CuisineTypeId: 1,
                Phone: null,
                Email: null,
                Description: "Good description",
                OwnerId: null,
                TicketId: null),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_CONTENT");
        _sets.Restaurants.Should().BeEmpty();
        _sets.AuditLogs.Should().BeEmpty();
    }
}
