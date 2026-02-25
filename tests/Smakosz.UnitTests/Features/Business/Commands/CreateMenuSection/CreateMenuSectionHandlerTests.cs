using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.CreateMenuSection;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Commands.CreateMenuSection;

[Trait("Category", "Handlers")]
public class CreateMenuSectionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly CreateMenuSectionHandler _handler;

    public CreateMenuSectionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _handler = new CreateMenuSectionHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_OwnerWithRestaurant_CreatesSectionWithCorrectName()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new CreateMenuSectionCommand("Starters"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.MenuSections.Should().ContainSingle(s => s.SectionName == "Starters" && s.RestaurantId == 1);
    }

    [Fact]
    public async Task Handle_OwnerWithRestaurant_ReturnsSectionId()
    {
        var restaurant = new Restaurant { RestaurantId = 5, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new CreateMenuSectionCommand("Main Course"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(0); // SectionId is default(int) since Add doesn't auto-generate in mock
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFound()
    {
        var result = await _handler.Handle(new CreateMenuSectionCommand("Desserts"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsInvalidCredentials()
    {
        var anonymousUser = MockExtensions.CreateAnonymousUser();
        var handler = new CreateMenuSectionHandler(_db, anonymousUser);

        var result = await handler.Handle(new CreateMenuSectionCommand("Beverages"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Handle_ExistingSections_SetsDisplayOrderAfterLast()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        _sets.MenuSections.Add(new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Starters", DisplayOrder = 1 });
        _sets.MenuSections.Add(new MenuSection { SectionId = 2, RestaurantId = 1, SectionName = "Mains", DisplayOrder = 2 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new CreateMenuSectionCommand("Desserts"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.MenuSections.Should().Contain(s => s.SectionName == "Desserts" && s.DisplayOrder == 3);
    }
}
