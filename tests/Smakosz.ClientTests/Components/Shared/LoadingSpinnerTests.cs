using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components.Shared;

public class LoadingSpinnerTests : BunitTestBase
{
    [Fact]
    public void RendersSpinner()
    {
        var cut = RenderComponent<LoadingSpinner>();

        cut.Find(".spinner-border").Should().NotBeNull();
        cut.Find(".visually-hidden").TextContent.Should().Be("Ladowanie...");
    }

    [Fact]
    public void WithText_RendersText()
    {
        var cut = RenderComponent<LoadingSpinner>(p => p.Add(c => c.Text, "Wczytywanie..."));
        cut.Find("p.text-muted").TextContent.Should().Be("Wczytywanie...");
    }

    [Fact]
    public void WithoutText_NoTextElement()
    {
        var cut = RenderComponent<LoadingSpinner>();
        cut.FindAll("p.text-muted").Should().BeEmpty();
    }
}
