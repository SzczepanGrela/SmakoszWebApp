using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class ReviewCardTests : BunitTestBase
{
    private static ReviewCardDto CreateReview() => new()
    {
        PublicId = Guid.NewGuid(),
        DishRating = 8,
        ServiceRating = 7,
        CleanlinessRating = 9,
        AmbianceRating = 6,
        Content = "Bardzo dobre danie!",
        CreatedAt = DateTime.UtcNow.AddHours(-2),
        Author = new UserSummaryDto
        {
            PublicId = Guid.NewGuid(),
            Slug = "jan-kowalski",
            Username = "JanKowalski",
            ReviewCount = 5
        },
        DishName = "Pizza Margherita",
        DishSlug = "pizza-margherita",
        RestaurantName = "Pizzeria Roma",
        RestaurantSlug = "pizzeria-roma"
    };

    [Fact]
    public void RendersAuthorAndContent()
    {
        var review = CreateReview();
        var cut = RenderComponent<ReviewCard>(p => p.Add(c => c.Review, review));

        cut.Markup.Should().Contain("JanKowalski");
        cut.Markup.Should().Contain("Bardzo dobre danie!");
    }

    [Fact]
    public void ShowDish_RendersDishLink()
    {
        var review = CreateReview();
        var cut = RenderComponent<ReviewCard>(p => p
            .Add(c => c.Review, review)
            .Add(c => c.ShowDish, true));

        cut.Markup.Should().Contain("Pizza Margherita");
        cut.Find("a[href='/dishes/pizza-margherita']").Should().NotBeNull();
    }

    [Fact]
    public void ShowDishFalse_NoDishLink()
    {
        var review = CreateReview();
        var cut = RenderComponent<ReviewCard>(p => p
            .Add(c => c.Review, review)
            .Add(c => c.ShowDish, false));

        cut.FindAll("a[href='/dish/pizza-margherita']").Should().BeEmpty();
    }

    [Fact]
    public void ShowRestaurant_RendersRestaurantName()
    {
        var review = CreateReview();
        var cut = RenderComponent<ReviewCard>(p => p
            .Add(c => c.Review, review)
            .Add(c => c.ShowRestaurant, true));

        cut.Markup.Should().Contain("Pizzeria Roma");
    }

    [Fact]
    public void NoContent_NoContentParagraph()
    {
        var review = CreateReview();
        review.Content = null;
        var cut = RenderComponent<ReviewCard>(p => p.Add(c => c.Review, review));

        cut.FindAll("p.mt-2").Should().BeEmpty();
    }

    [Fact]
    public void ShowActionsWithAuth_ReportButtonVisible()
    {
        SetAuthenticatedUser("testuser", "User");
        var review = CreateReview();

        var cut = RenderComponent<ReviewCard>(p => p
            .Add(c => c.Review, review)
            .Add(c => c.ShowActions, true));

        cut.FindAll("button[title='Zgłoś recenzję']").Should().NotBeEmpty();
    }

    [Fact]
    public void ShowActionsAnonymous_ReportButtonHidden()
    {
        var review = CreateReview();
        var cut = RenderComponent<ReviewCard>(p => p
            .Add(c => c.Review, review)
            .Add(c => c.ShowActions, true));

        cut.FindAll("button[title='Zgłoś recenzję']").Should().BeEmpty();
    }

    [Fact]
    public async Task OpenReportModal_LoadsReasons()
    {
        SetAuthenticatedUser("testuser", "User");
        var review = CreateReview();

        var reviewService = Services.GetRequiredService<IReviewService>();
        reviewService.GetReportReasonsAsync().Returns(new List<ReportReasonDto>
        {
            new() { ReasonCode = "spam", LabelPl = "Spam" },
            new() { ReasonCode = "offensive", LabelPl = "Obrazliwe" }
        });

        var cut = RenderComponent<ReviewCard>(p => p
            .Add(c => c.Review, review)
            .Add(c => c.ShowActions, true));

        cut.Find("button[title='Zgłoś recenzję']").Click();

        cut.WaitForState(() => cut.Markup.Contains("Spam"));
        cut.Markup.Should().Contain("Spam");
        cut.Markup.Should().Contain("Obrazliwe");
    }

    [Fact]
    public async Task SubmitReport_CallsServiceAndShowsToast()
    {
        SetAuthenticatedUser("testuser", "User");
        var review = CreateReview();

        var reviewService = Services.GetRequiredService<IReviewService>();
        reviewService.GetReportReasonsAsync().Returns(new List<ReportReasonDto>
        {
            new() { ReasonCode = "spam", LabelPl = "Spam" }
        });
        reviewService.ReportReviewAsync(review.PublicId, Arg.Any<List<string>>(), Arg.Any<string?>())
            .Returns(true);

        var cut = RenderComponent<ReviewCard>(p => p
            .Add(c => c.Review, review)
            .Add(c => c.ShowActions, true));

        cut.Find("button[title='Zgłoś recenzję']").Click();
        cut.WaitForState(() => cut.Markup.Contains("Spam"));

        cut.Find("input.form-check-input").Change(true);

        cut.Find("button.btn-danger.btn-sm").Click();

        await reviewService.Received(1).ReportReviewAsync(
            review.PublicId,
            Arg.Is<List<string>>(l => l.Contains("spam")),
            Arg.Any<string?>());
    }
}
