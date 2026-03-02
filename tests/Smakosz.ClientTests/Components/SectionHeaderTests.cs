using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class SectionHeaderTests : BunitTestBase
{
    [Fact]
    public void RendersTitle()
    {
        var cut = RenderComponent<SectionHeader>(p => p.Add(c => c.Title, "Popularne"));
        cut.Find("h2").TextContent.Should().Contain("Popularne");
    }

    [Fact]
    public void WithIcon_RendersIcon()
    {
        var cut = RenderComponent<SectionHeader>(p => p
            .Add(c => c.Title, "Test")
            .Add(c => c.Icon, "fa-solid fa-fire"));

        cut.Find("i.fa-solid.fa-fire").Should().NotBeNull();
    }

    [Fact]
    public void WithoutIcon_NoIconElement()
    {
        var cut = RenderComponent<SectionHeader>(p => p.Add(c => c.Title, "Test"));
        cut.FindAll("i").Should().BeEmpty();
    }

    [Fact]
    public void WithSubtitle_RendersSubtitle()
    {
        var cut = RenderComponent<SectionHeader>(p => p
            .Add(c => c.Title, "Test")
            .Add(c => c.Subtitle, "Podtytul"));

        cut.Find("p.text-muted").TextContent.Should().Be("Podtytul");
    }

    [Fact]
    public void WithoutSubtitle_NoSubtitleElement()
    {
        var cut = RenderComponent<SectionHeader>(p => p.Add(c => c.Title, "Test"));
        cut.FindAll("p.text-muted").Should().BeEmpty();
    }
}
