using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components.Shared;

public class RatingStarsTests : BunitTestBase
{
    [Fact]
    public void Rating10_AllStarsFull()
    {
        var cut = RenderComponent<RatingStars>(p => p.Add(c => c.Rating, 10.0));

        cut.FindAll("i.fa-solid.fa-star.text-warning").Should().HaveCount(10);
        cut.FindAll("i.fa-regular.fa-star").Should().BeEmpty();
    }

    [Fact]
    public void Rating0_AllStarsEmpty()
    {
        var cut = RenderComponent<RatingStars>(p => p.Add(c => c.Rating, 0.0));

        cut.FindAll("i.fa-regular.fa-star").Should().HaveCount(10);
    }

    [Fact]
    public void HalfRating_ShowsHalfStar()
    {
        var cut = RenderComponent<RatingStars>(p => p.Add(c => c.Rating, 5.5));

        cut.FindAll("i.fa-solid.fa-star.text-warning").Should().HaveCount(5);
        cut.FindAll("i.fa-solid.fa-star-half-stroke").Should().HaveCount(1);
        cut.FindAll("i.fa-regular.fa-star").Should().HaveCount(4);
    }

    [Fact]
    public void ShowValueTrue_DisplaysNumericValue()
    {
        var cut = RenderComponent<RatingStars>(p => p
            .Add(c => c.Rating, 7.5)
            .Add(c => c.ShowValue, true));

        cut.Markup.Should().Contain(7.5.ToString("F1"));
    }

    [Fact]
    public void ShowValueFalse_NoNumericValue()
    {
        var cut = RenderComponent<RatingStars>(p => p
            .Add(c => c.Rating, 7.5)
            .Add(c => c.ShowValue, false));

        cut.FindAll("span.fw-bold").Should().BeEmpty();
    }

    [Fact]
    public void SizeLg_AppliesLargeClass()
    {
        var cut = RenderComponent<RatingStars>(p => p
            .Add(c => c.Rating, 5.0)
            .Add(c => c.Size, "lg"));

        cut.Find(".rating-stars").ClassList.Should().Contain("stars-lg");
        cut.FindAll("i.fa-lg").Should().NotBeEmpty();
    }
}
