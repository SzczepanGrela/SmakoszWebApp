using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class FilterPanelTests : BunitTestBase
{
    [Fact]
    public void RendersTitle()
    {
        var cut = RenderComponent<FilterPanel>();
        cut.Find("h5").TextContent.Should().Contain("Filtry");
    }

    [Fact]
    public void RendersChildContent()
    {
        var cut = RenderComponent<FilterPanel>(p => p
            .AddChildContent("<div class='my-filter'>Test Filter</div>"));

        cut.Markup.Should().Contain("Test Filter");
    }

    [Fact]
    public void IsCollapsible_AddsCollapseClass()
    {
        var cut = RenderComponent<FilterPanel>(p => p.Add(c => c.IsCollapsible, true));
        cut.Find(".filters-panel").ClassList.Should().Contain("collapse-panel");
    }

    [Fact]
    public void NotCollapsible_NoCollapseClass()
    {
        var cut = RenderComponent<FilterPanel>(p => p.Add(c => c.IsCollapsible, false));
        cut.Find(".filters-panel").ClassList.Should().NotContain("collapse-panel");
    }
}
