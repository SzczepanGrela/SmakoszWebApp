using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class CategoryCardTests : BunitTestBase
{
    [Fact]
    public void RendersNameAndLink()
    {
        var cut = RenderComponent<CategoryCard>(p => p.Add(c => c.Name, "Pizza"));

        cut.Find("a").GetAttribute("href").Should().Be("/search?cuisines=Pizza&type=dishes");
        cut.Markup.Should().Contain("Pizza");
    }

    [Fact]
    public void WithIcon_RendersIcon()
    {
        var cut = RenderComponent<CategoryCard>(p => p
            .Add(c => c.Name, "Pizza")
            .Add(c => c.Icon, "\U0001F355"));

        cut.Find("span.fs-4").TextContent.Should().Contain("\U0001F355");
    }

    [Fact]
    public void WithoutIcon_NoIconSpan()
    {
        var cut = RenderComponent<CategoryCard>(p => p.Add(c => c.Name, "Pizza"));
        cut.FindAll("span.fs-4").Should().BeEmpty();
    }
}
