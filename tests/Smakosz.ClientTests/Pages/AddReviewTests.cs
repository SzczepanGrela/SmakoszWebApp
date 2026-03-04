using Microsoft.AspNetCore.Components;
using Smakosz.Client.Pages.User;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Pages;

public class AddReviewTests : BunitTestBase
{
    public AddReviewTests()
    {
        SetAuthenticatedUser("testuser", "User");
    }

    private static DishDetailDto CreateDish() => new()
    {
        PublicId = Guid.NewGuid(),
        Slug = "pizza-margherita",
        DishName = "Pizza Margherita",
        RestaurantName = "Pizzeria Roma",
        CityName = "Warszawa"
    };

    private IRenderedComponent<AddReview> RenderWithDishSlug(string slug)
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo($"/review/add?dish={slug}");
        return RenderComponent<AddReview>();
    }

    [Fact]
    public void WithDishSlug_LoadsDish()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        var dish = CreateDish();
        dishService.GetBySlugAsync("pizza-margherita").Returns(dish);

        var cut = RenderWithDishSlug("pizza-margherita");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));
        cut.Markup.Should().Contain("Pizza Margherita");
        cut.Markup.Should().Contain("Pizzeria Roma");
    }

    [Fact]
    public void WithoutDishSlug_ShowsSearchForm()
    {
        var cut = RenderComponent<AddReview>();

        cut.Markup.Should().Contain("Wyszukaj danie");
        cut.Find("input[placeholder='Wpisz nazwe dania...']").Should().NotBeNull();
    }

    [Fact]
    public void WithDish_ShowsRatingInputs()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());

        var cut = RenderWithDishSlug("pizza-margherita");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.Markup.Should().Contain("Ocena dania");
        cut.Markup.Should().Contain("Obsluga");
        cut.Markup.Should().Contain("Czystosc");
        cut.Markup.Should().Contain("Atmosfera");
    }

    [Fact]
    public void SubmitWithoutRatings_ShowsError()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());

        var cut = RenderWithDishSlug("pizza-margherita");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.Find("button.btn-primary.btn-lg").Click();

        cut.Markup.Should().Contain("Ocena dania jest wymagana");
    }

    [Fact]
    public async Task SuccessfulSubmit_NavigatesToDish()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        var reviewService = Services.GetRequiredService<IReviewService>();
        var dish = CreateDish();
        dishService.GetBySlugAsync("pizza-margherita").Returns(dish);
        reviewService.CreateAsync(Arg.Any<CreateReviewDto>())
            .Returns(new ApiResponse<ReviewCardDto> { Success = true });

        var cut = RenderWithDishSlug("pizza-margherita");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        var stars = cut.FindAll("i.interactive-star");
        stars[4].Click();   // DishRating = 5
        stars[14].Click();  // ServiceRating = 5
        stars[24].Click();  // CleanlinessRating = 5
        stars[34].Click();  // AmbianceRating = 5

        cut.Find("button.btn-primary.btn-lg").Click();

        await reviewService.Received(1).CreateAsync(Arg.Is<CreateReviewDto>(r =>
            r.DishRating == 5 && r.ServiceRating == 5));

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.Uri.Should().Contain("/dish/pizza-margherita");
    }

    [Fact]
    public async Task FailedSubmit_ShowsError()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        var reviewService = Services.GetRequiredService<IReviewService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());
        reviewService.CreateAsync(Arg.Any<CreateReviewDto>())
            .Returns(new ApiResponse<ReviewCardDto>
            {
                Success = false,
                Error = new ApiError { Message = "Juz oceniles to danie." }
            });

        var cut = RenderWithDishSlug("pizza-margherita");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        var stars = cut.FindAll("i.interactive-star");
        stars[4].Click();
        stars[14].Click();
        stars[24].Click();
        stars[34].Click();

        cut.Find("button.btn-primary.btn-lg").Click();

        cut.WaitForState(() => cut.Markup.Contains("Juz oceniles to danie."));
    }

    [Fact]
    public void CancelWithDish_NavigatesToDish()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());

        var cut = RenderWithDishSlug("pizza-margherita");

        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.Find("button.btn-outline-secondary").Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.Uri.Should().Contain("/dish/pizza-margherita");
    }
}
