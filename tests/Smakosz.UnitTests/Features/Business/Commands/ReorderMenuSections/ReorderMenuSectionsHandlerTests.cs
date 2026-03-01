using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.ReorderMenuSections;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.ReorderMenuSections;

[Trait("Category", "Handlers")]
public class ReorderMenuSectionsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly ReorderMenuSectionsHandler _handler;

    public ReorderMenuSectionsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new ReorderMenuSectionsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesDisplayOrder()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        _sets.Restaurants.Add(restaurant);
        _sets.MenuSections.Add(new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "A", DisplayOrder = 1 });
        _sets.MenuSections.Add(new MenuSection { SectionId = 2, RestaurantId = 1, SectionName = "B", DisplayOrder = 2 });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new ReorderMenuSectionsCommand(new List<int> { 2, 1 }), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.MenuSections.First(s => s.SectionId == 2).DisplayOrder.Should().Be(1);
        _sets.MenuSections.First(s => s.SectionId == 1).DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NoRestaurant_ReturnsError()
    {
        var result = await _handler.Handle(
            new ReorderMenuSectionsCommand(new List<int> { 1, 2 }), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("RESTAURANT_NOT_FOUND");
    }
}
