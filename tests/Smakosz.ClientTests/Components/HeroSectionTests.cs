using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class HeroSectionTests : BunitTestBase
{
    [Fact]
    public void RendersBackgroundUrl()
    {
        var cut = RenderComponent<HeroSection>(p => p
            .Add(c => c.BackgroundUrl, "/img/hero.jpg"));

        cut.Find(".hero-section").GetAttribute("style")
            .Should().Contain("background-image:url('/img/hero.jpg')");
    }

    [Fact]
    public void NullBackgroundUrl_NoBackgroundStyle()
    {
        var cut = RenderComponent<HeroSection>();

        cut.Find(".hero-section").GetAttribute("style")
            .Should().BeEmpty();
    }

    [Fact]
    public void RendersCreditText()
    {
        var cut = RenderComponent<HeroSection>(p => p
            .Add(c => c.CreditText, "Photo by John"));

        cut.Markup.Should().Contain("Photo by John");
    }

    [Fact]
    public void NoCreditText_HidesCreditSpan()
    {
        var cut = RenderComponent<HeroSection>();

        cut.FindAll("span.position-absolute").Should().BeEmpty();
    }

    [Fact]
    public void RendersChildContent()
    {
        var cut = RenderComponent<HeroSection>(p => p
            .AddChildContent("<h1>Welcome</h1>"));

        cut.Markup.Should().Contain("<h1>Welcome</h1>");
    }
}
