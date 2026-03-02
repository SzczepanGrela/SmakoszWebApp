using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components.Shared;

public class EmptyStateTests : BunitTestBase
{
    [Fact]
    public void RendersTitleAndIcon()
    {
        var cut = RenderComponent<EmptyState>(p => p
            .Add(c => c.Title, "Brak wynikow")
            .Add(c => c.Icon, "fa-solid fa-search"));

        cut.Find("h5").TextContent.Should().Be("Brak wynikow");
        cut.Find("i.fa-solid.fa-search").Should().NotBeNull();
    }

    [Fact]
    public void WithDescription_RendersDescription()
    {
        var cut = RenderComponent<EmptyState>(p => p
            .Add(c => c.Title, "Pusto")
            .Add(c => c.Description, "Nie znaleziono wynikow"));

        cut.Find("p.text-muted").TextContent.Should().Be("Nie znaleziono wynikow");
    }

    [Fact]
    public void WithoutDescription_HidesDescription()
    {
        var cut = RenderComponent<EmptyState>(p => p.Add(c => c.Title, "Pusto"));
        cut.FindAll("p.text-muted").Should().BeEmpty();
    }

    [Fact]
    public void WithAction_RendersLink()
    {
        var cut = RenderComponent<EmptyState>(p => p
            .Add(c => c.Title, "Pusto")
            .Add(c => c.ActionText, "Przejdz")
            .Add(c => c.ActionUrl, "/home"));

        var link = cut.Find("a.btn.btn-primary");
        link.TextContent.Should().Be("Przejdz");
        link.GetAttribute("href").Should().Be("/home");
    }

    [Fact]
    public void WithoutAction_HidesLink()
    {
        var cut = RenderComponent<EmptyState>(p => p.Add(c => c.Title, "Pusto"));
        cut.FindAll("a.btn").Should().BeEmpty();
    }
}
