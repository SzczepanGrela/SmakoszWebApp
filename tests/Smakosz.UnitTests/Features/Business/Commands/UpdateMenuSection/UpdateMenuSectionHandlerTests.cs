using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateMenuSection;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateMenuSection;

[Trait("Category", "Handlers")]
public class UpdateMenuSectionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateMenuSectionHandler _handler;

    public UpdateMenuSectionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new UpdateMenuSectionHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_UpdatesSectionName()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var section = new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Zupy", Restaurant = restaurant };
        _sets.MenuSections.Add(section);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateMenuSectionCommand(1, "Desery"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        section.SectionName.Should().Be("Desery");
    }

    [Fact]
    public async Task Handle_NotOwner_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 999;
        var section = new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Zupy", Restaurant = restaurant };
        _sets.MenuSections.Add(section);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateMenuSectionCommand(1, "Desery"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MENU_SECTION_NOT_OWNER");
    }

    [Fact]
    public async Task Handle_NotFound_ReturnsError()
    {
        var result = await _handler.Handle(new UpdateMenuSectionCommand(999, "Desery"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("MENU_SECTION_NOT_FOUND");
    }
}
