using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.DeleteMenuSection;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.DeleteMenuSection;

[Trait("Category", "Handlers")]
public class DeleteMenuSectionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly DeleteMenuSectionHandler _handler;

    public DeleteMenuSectionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new DeleteMenuSectionHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_RemovesSection()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var section = new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Zupy", Restaurant = restaurant };
        _sets.MenuSections.Add(section);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteMenuSectionCommand(1), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.MenuSections.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 999;
        var section = new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Zupy", Restaurant = restaurant };
        _sets.MenuSections.Add(section);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new DeleteMenuSectionCommand(1), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MENU_SECTION_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new DeleteMenuSectionCommand(999), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MENU_SECTION_NOT_FOUND");
    }
}
