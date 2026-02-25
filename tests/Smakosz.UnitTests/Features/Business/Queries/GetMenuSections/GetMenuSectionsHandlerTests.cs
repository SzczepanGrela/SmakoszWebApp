using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Queries.GetMenuSections;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Business.Queries.GetMenuSections;

[Trait("Category", "Handlers")]
public class GetMenuSectionsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetMenuSectionsHandler _handler;

    public GetMenuSectionsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 10, role: "Business");
        _handler = new GetMenuSectionsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_OwnerWithSections_ReturnsSectionsForRestaurant()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        _sets.MenuSections.Add(new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Starters", DisplayOrder = 1 });
        _sets.MenuSections.Add(new MenuSection { SectionId = 2, RestaurantId = 1, SectionName = "Mains", DisplayOrder = 2 });
        _sets.MenuSections.Add(new MenuSection { SectionId = 3, RestaurantId = 99, SectionName = "Other", DisplayOrder = 1 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetMenuSectionsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().HaveCount(2);
        result.Value.Should().AllSatisfy(s => s.MenuSectionId.Should().BeOneOf(1, 2));
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsNotFound()
    {
        var result = await _handler.Handle(new GetMenuSectionsQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NoSections_ReturnsEmptyList()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetMenuSectionsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_SectionDtoFields_MappedCorrectly()
    {
        var restaurant = new Restaurant { RestaurantId = 1, OwnerId = 10, RestaurantName = "My Restaurant", Slug = "my-restaurant" };
        _sets.Restaurants.Add(restaurant);
        _sets.MenuSections.Add(new MenuSection { SectionId = 5, RestaurantId = 1, SectionName = "Desserts", DisplayOrder = 3 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetMenuSectionsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        var section = result.Value.Single();
        section.MenuSectionId.Should().Be(5);
        section.Name.Should().Be("Desserts");
        section.SortOrder.Should().Be(3);
    }
}
