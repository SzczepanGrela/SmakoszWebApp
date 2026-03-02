using Smakosz.ClientTests.Common;
using Smakosz.Client.Ops.Components;

namespace Smakosz.ClientTests.Components.Ops;

public class AdminStatCardTests : BunitTestBase
{
    [Fact]
    public void RendersAllProperties()
    {
        var cut = RenderComponent<AdminStatCard>(p => p
            .Add(c => c.Icon, "fa-solid fa-users")
            .Add(c => c.Value, "42")
            .Add(c => c.Label, "Uzytkownicy")
            .Add(c => c.Color, "#ff6600"));

        cut.Find("i.fa-solid.fa-users").Should().NotBeNull();
        cut.Find(".fs-3.fw-bold").TextContent.Should().Be("42");
        cut.Find("small.text-muted").TextContent.Should().Be("Uzytkownicy");
        cut.Markup.Should().Contain("#ff6600");
    }

    [Fact]
    public void DefaultValues_RenderDefaults()
    {
        var cut = RenderComponent<AdminStatCard>();

        cut.Find(".fs-3.fw-bold").TextContent.Should().Be("0");
        cut.Find("i.fa-solid.fa-chart-line").Should().NotBeNull();
    }
}
