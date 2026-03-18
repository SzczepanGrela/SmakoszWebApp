using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class MenuSectionEditorTests : BunitTestBase
{
    [Fact]
    public void EmptySections_ShowsOnlyAddInput()
    {
        var cut = RenderComponent<MenuSectionEditor>(p => p
            .Add(c => c.Sections, new List<MenuSectionDto>()));

        cut.FindAll(".list-group-item").Should().BeEmpty();
        cut.Find("input[placeholder='Nowa sekcja menu...']").Should().NotBeNull();
    }

    [Fact]
    public void WithSections_RendersSectionNames()
    {
        var sections = new List<MenuSectionDto>
        {
            new() { SectionName = "Zupy", DisplayOrder = 1 },
            new() { SectionName = "Dania glowne", DisplayOrder = 2 }
        };

        var cut = RenderComponent<MenuSectionEditor>(p => p
            .Add(c => c.Sections, sections));

        cut.Markup.Should().Contain("Zupy");
        cut.Markup.Should().Contain("Dania glowne");
        cut.FindAll(".list-group-item").Should().HaveCount(2);
    }

    [Fact]
    public void AddSection_AddsSectionToList()
    {
        var sections = new List<MenuSectionDto>();
        List<MenuSectionDto>? changedSections = null;

        var cut = RenderComponent<MenuSectionEditor>(p => p
            .Add(c => c.Sections, sections)
            .Add(c => c.SectionsChanged, (List<MenuSectionDto> s) => changedSections = s));

        var input = cut.Find("input[placeholder='Nowa sekcja menu...']");
        input.Change("Desery");
        cut.Find("button.btn-primary").Click();

        changedSections.Should().NotBeNull();
        changedSections.Should().Contain(s => s.SectionName == "Desery");
    }

    [Fact]
    public void RemoveSection_RemovesFromList()
    {
        var sections = new List<MenuSectionDto>
        {
            new() { SectionName = "Zupy", DisplayOrder = 1 },
            new() { SectionName = "Desery", DisplayOrder = 2 }
        };
        List<MenuSectionDto>? changedSections = null;

        var cut = RenderComponent<MenuSectionEditor>(p => p
            .Add(c => c.Sections, sections)
            .Add(c => c.SectionsChanged, (List<MenuSectionDto> s) => changedSections = s));

        cut.FindAll("button.btn-outline-danger")[0].Click();

        changedSections.Should().HaveCount(1);
        changedSections![0].SectionName.Should().Be("Desery");
    }

    [Fact]
    public void MoveUp_FirstItem_Disabled()
    {
        var sections = new List<MenuSectionDto>
        {
            new() { SectionName = "Zupy", DisplayOrder = 1 },
            new() { SectionName = "Desery", DisplayOrder = 2 }
        };

        var cut = RenderComponent<MenuSectionEditor>(p => p
            .Add(c => c.Sections, sections));

        var moveUpButtons = cut.FindAll(".btn-group button");
        moveUpButtons[0].HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void MoveDown_SwapsSections()
    {
        var sections = new List<MenuSectionDto>
        {
            new() { SectionName = "Zupy", DisplayOrder = 1 },
            new() { SectionName = "Desery", DisplayOrder = 2 }
        };
        List<MenuSectionDto>? changedSections = null;

        var cut = RenderComponent<MenuSectionEditor>(p => p
            .Add(c => c.Sections, sections)
            .Add(c => c.SectionsChanged, (List<MenuSectionDto> s) => changedSections = s));

        var btnGroups = cut.FindAll(".btn-group.btn-group-sm");
        var moveDownBtn = btnGroups[0].QuerySelectorAll("button")[1];
        moveDownBtn.Click();

        changedSections.Should().NotBeNull();
        changedSections![0].SectionName.Should().Be("Desery");
        changedSections[1].SectionName.Should().Be("Zupy");
    }

    [Fact]
    public void EditSection_SavesNewName()
    {
        var sections = new List<MenuSectionDto>
        {
            new() { SectionName = "Zupy", DisplayOrder = 1 }
        };
        List<MenuSectionDto>? changedSections = null;

        var cut = RenderComponent<MenuSectionEditor>(p => p
            .Add(c => c.Sections, sections)
            .Add(c => c.SectionsChanged, (List<MenuSectionDto> s) => changedSections = s));

        cut.Find("button.btn-outline-primary").Click();

        cut.Find("input.form-control-sm").Change("Zupy dnia");

        cut.Find("button.btn-success").Click();

        changedSections.Should().NotBeNull();
        changedSections![0].SectionName.Should().Be("Zupy dnia");
    }
}
