using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class RatingInputTests : BunitTestBase
{
    [Fact]
    public void Renders10Stars()
    {
        var cut = RenderComponent<RatingInput>(p => p.Add(c => c.Value, 0));
        cut.FindAll("i.interactive-star").Should().HaveCount(10);
    }

    [Fact]
    public void Value5_FirstFiveStarsHighlighted()
    {
        var cut = RenderComponent<RatingInput>(p => p.Add(c => c.Value, 5));

        var stars = cut.FindAll("i.interactive-star");
        stars.Take(5).Should().AllSatisfy(s => s.ClassList.Should().Contain("text-warning"));
        stars.Skip(5).Should().AllSatisfy(s => s.ClassList.Should().Contain("text-muted"));
    }

    [Fact]
    public void DisplaysValueOutOfTen()
    {
        var cut = RenderComponent<RatingInput>(p => p.Add(c => c.Value, 7));
        cut.Markup.Should().Contain("7/10");
    }

    [Fact]
    public void ClickStar_InvokesValueChanged()
    {
        int? newValue = null;
        var cut = RenderComponent<RatingInput>(p => p
            .Add(c => c.Value, 0)
            .Add(c => c.ValueChanged, (int v) => newValue = v));

        cut.FindAll("i.interactive-star")[4].Click(); // 5th star
        newValue.Should().Be(5);
    }

    [Fact]
    public void WithLabel_RendersLabel()
    {
        var cut = RenderComponent<RatingInput>(p => p
            .Add(c => c.Value, 0)
            .Add(c => c.Label, "Ocena dania"));

        cut.Find("label.form-label").TextContent.Should().Be("Ocena dania");
    }

    [Fact]
    public void WithoutLabel_NoLabelElement()
    {
        var cut = RenderComponent<RatingInput>(p => p.Add(c => c.Value, 0));
        cut.FindAll("label").Should().BeEmpty();
    }
}
