using Smakosz.Client.Components;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class FilterChipTests : BunitTestBase
{
    [Fact]
    public void RendersLabel()
    {
        var cut = RenderComponent<FilterChip>(p => p.Add(c => c.Label, "Włoska"));

        cut.Markup.Should().Contain("Włoska");
    }

    [Fact]
    public void ClickRemove_InvokesCallback()
    {
        var invoked = false;
        var cut = RenderComponent<FilterChip>(p => p
            .Add(c => c.Label, "Wegan")
            .Add(c => c.OnRemove, () => invoked = true));

        cut.Find(".filter-chip-remove").Click();

        invoked.Should().BeTrue();
    }
}
