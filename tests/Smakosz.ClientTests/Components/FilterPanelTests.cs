using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class FilterPanelTests : BunitTestBase
{
    [Fact]
    public void RendersTitle()
    {
        var cut = RenderComponent<FilterPanel>(p => p.Add(c => c.StartCollapsed, false));
        cut.Find("h5").TextContent.Should().Contain("Filtry");
    }

    [Fact]
    public void RendersChildContent_WhenExpanded()
    {
        var cut = RenderComponent<FilterPanel>(p => p
            .Add(c => c.StartCollapsed, false)
            .AddChildContent("<div class='my-filter'>Test Filter</div>"));

        cut.Markup.Should().Contain("Test Filter");
    }

    [Fact]
    public void HidesChildContent_WhenCollapsed()
    {
        var cut = RenderComponent<FilterPanel>(p => p
            .Add(c => c.StartCollapsed, true)
            .AddChildContent("<div class='my-filter'>Test Filter</div>"));

        cut.Markup.Should().NotContain("Test Filter");
    }

    [Fact]
    public void ToggleExpandsCollapsedPanel()
    {
        var cut = RenderComponent<FilterPanel>(p => p
            .Add(c => c.StartCollapsed, true)
            .AddChildContent("<div class='my-filter'>Test Filter</div>"));

        cut.Markup.Should().NotContain("Test Filter");

        cut.Find(".cursor-pointer").Click();

        cut.Markup.Should().Contain("Test Filter");
    }
}
