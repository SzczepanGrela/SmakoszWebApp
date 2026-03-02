using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class DietaryBadgesTests : BunitTestBase
{
    [Fact]
    public void NoFlags_RendersNothing()
    {
        var cut = RenderComponent<DietaryBadges>();
        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void VeganFlag_RendersBadge()
    {
        var cut = RenderComponent<DietaryBadges>(p => p.Add(c => c.IsVegan, true));
        cut.Markup.Should().Contain("Vegan");
        cut.FindAll(".badge.bg-success").Should().HaveCount(1);
    }

    [Fact]
    public void VegetarianFlag_RendersBadge()
    {
        var cut = RenderComponent<DietaryBadges>(p => p.Add(c => c.IsVegetarian, true));
        cut.Markup.Should().Contain("Wege");
    }

    [Fact]
    public void GlutenFreeFlag_RendersBadge()
    {
        var cut = RenderComponent<DietaryBadges>(p => p.Add(c => c.IsGlutenFree, true));
        cut.Markup.Should().Contain("Bez glutenu");
        cut.Find(".badge.bg-info").Should().NotBeNull();
    }

    [Fact]
    public void LactoseFreeFlag_RendersBadge()
    {
        var cut = RenderComponent<DietaryBadges>(p => p.Add(c => c.IsLactoseFree, true));
        cut.Markup.Should().Contain("Bez laktozy");
    }

    [Fact]
    public void AllFlags_RendersAllBadges()
    {
        var cut = RenderComponent<DietaryBadges>(p => p
            .Add(c => c.IsVegan, true)
            .Add(c => c.IsVegetarian, true)
            .Add(c => c.IsGlutenFree, true)
            .Add(c => c.IsLactoseFree, true));

        cut.FindAll(".badge").Should().HaveCount(4);
    }

    [Fact]
    public void TagsWithDisplayColor_RenderCustomColorBadge()
    {
        var tags = new List<TagDto>
        {
            new() { TagName = "Pikantne", DisplayColor = "#ff0000" }
        };

        var cut = RenderComponent<DietaryBadges>(p => p.Add(c => c.Tags, tags));

        var badge = cut.Find(".badge");
        badge.TextContent.Should().Contain("Pikantne");
        badge.GetAttribute("style").Should().Contain("background-color:#ff0000");
    }

    [Fact]
    public void TagsWithoutDisplayColor_RenderSecondaryBadge()
    {
        var tags = new List<TagDto>
        {
            new() { TagName = "Popularne" }
        };

        var cut = RenderComponent<DietaryBadges>(p => p.Add(c => c.Tags, tags));

        cut.Find(".badge.bg-secondary").TextContent.Should().Contain("Popularne");
    }
}
