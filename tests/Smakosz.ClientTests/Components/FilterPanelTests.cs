using Smakosz.Client.Components;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class FilterPanelTests : BunitTestBase
{
    [Fact]
    public void RendersHeaderWithBadge_WhenActiveCount()
    {
        var cut = RenderComponent<FilterPanel>(p => p
            .Add(c => c.TotalActiveCount, 3));

        cut.Find(".filter-panel-badge").TextContent.Trim().Should().Be("3");
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
