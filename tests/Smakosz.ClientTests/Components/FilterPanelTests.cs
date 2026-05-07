using Smakosz.Client.Components;
using Smakosz.Client.Models;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class FilterPanelTests : BunitTestBase
{
    [Fact]
    public void RendersHeaderWithBadge_WhenActiveFilters()
    {
        var cut = RenderComponent<FilterPanel>(p => p
            .Add(c => c.TotalActiveCount, 3)
            .Add(c => c.ActiveFilters, new List<ActiveFilterDto>
            {
                new("cuisines", "Polska", "Polska"),
                new("cuisines", "Włoska", "Włoska"),
                new("dietary", "vegan", "Wegańskie")
            }));

        cut.Find(".filter-panel-badge").TextContent.Trim().Should().Be("3");
        cut.FindAll(".filter-chip").Count.Should().Be(3);
    }

    [Fact]
    public void ClickApply_InvokesCallback()
    {
        var invoked = false;
        var cut = RenderComponent<FilterPanel>(p => p
            .Add(c => c.OnApply, () => invoked = true));

        cut.Find(".filter-btn-apply").Click();

        invoked.Should().BeTrue();
    }

    [Fact]
    public void IsDirty_AppliesPulseClass()
    {
        var cut = RenderComponent<FilterPanel>(p => p
            .Add(c => c.IsDirty, true));

        cut.Find(".filter-btn-apply").ClassList.Should().Contain("dirty");
    }
}
