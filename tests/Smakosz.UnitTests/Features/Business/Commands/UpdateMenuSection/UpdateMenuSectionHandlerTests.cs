using FluentAssertions;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Business.Commands.UpdateMenuSection;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Business.Commands.UpdateMenuSection;

[Trait("Category", "Handlers")]
public class UpdateMenuSectionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly IForbiddenWordService _forbiddenWords;
    private readonly UpdateMenuSectionHandler _handler;

    public UpdateMenuSectionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _forbiddenWords = Substitute.For<IForbiddenWordService>();
        _handler = new UpdateMenuSectionHandler(_db, _currentUser, _forbiddenWords);
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesEditRequestInsteadOfDirectEdit()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var section = new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Zupy", Restaurant = restaurant };
        _sets.MenuSections.Add(section);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new UpdateMenuSectionCommand(1, "Desery"), CancellationToken.None);

        result.IsError.Should().BeFalse();
        section.SectionName.Should().Be("Zupy", "name should not change directly - goes through EditRequest");
        _sets.RestaurantEditRequests.Should().ContainSingle();
        var editRequest = _sets.RestaurantEditRequests[0];
        editRequest.NewName.Should().Be("Desery");
        editRequest.ChangeScope.Should().Be(EditRequestChangeScope.Section);
        editRequest.TargetEntityId.Should().Be(1);
        editRequest.ModerationStatus.Should().Be(ContentModerationStatus.Pending);
        _sets.SystemTickets.Should().ContainSingle();
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

    [Fact]
    public async Task Handle_ForbiddenWordInName_ReturnsError()
    {
        var restaurant = new RestaurantBuilder().WithId(1).Build();
        restaurant.OwnerId = 1;
        var section = new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Zupy", Restaurant = restaurant };
        _sets.MenuSections.Add(section);
        DbContextMockFactory.Refresh(_db, _sets);
        _forbiddenWords.ContainsAsync("Bad Name", Arg.Any<CancellationToken>(),
            Arg.Any<ForbiddenWordCategory[]>()).Returns(true);

        var result = await _handler.Handle(new UpdateMenuSectionCommand(1, "Bad Name"), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("FORBIDDEN_WORD_CONTENT");
    }
}
