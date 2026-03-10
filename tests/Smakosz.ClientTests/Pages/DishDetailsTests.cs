using Smakosz.Client.Pages.Public;
using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Pages;

public class DishDetailsTests : BunitTestBase
{
    private static DishDetailDto CreateDish() => new()
    {
        PublicId = Guid.NewGuid(),
        Slug = "pizza-margherita",
        DishName = "Pizza Margherita",
        Price = 29.99m,
        AvgRating = 8.5,
        ReviewCount = 42,
        Description = "Klasyczna pizza z mozzarella",
        RestaurantName = "Pizzeria Roma",
        RestaurantSlug = "pizzeria-roma",
        CuisineType = "Wloska",
        CityName = "Warszawa",
        IsVegan = false,
        IsVegetarian = true,
        IsGlutenFree = false,
        IsLactoseFree = false,
        IsSpicy = false,
        IsAvailable = true,
        IsSaved = false,
        Calories = 850
    };

    private static PagedResult<ReviewCardDto> CreateReviews() => new()
    {
        Data =
        [
            new ReviewCardDto
            {
                PublicId = Guid.NewGuid(),
                DishRating = 9, ServiceRating = 8, CleanlinessRating = 9, AmbianceRating = 7,
                Content = "Pyszna pizza!",
                Author = new UserSummaryDto { Slug = "jan", Username = "Jan" },
                DishName = "Pizza Margherita", DishSlug = "pizza-margherita",
                RestaurantName = "Roma", RestaurantSlug = "pizzeria-roma"
            }
        ],
        Pagination = new PaginationInfo { Page = 1, TotalPages = 1, TotalCount = 1 }
    };

    [Fact]
    public void LoadingState_ShowsSpinner()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        dishService.GetBySlugAsync("pizza").Returns(new TaskCompletionSource<DishDetailDto?>().Task);

        var cut = RenderComponent<DishDetails>(p => p.Add(c => c.Slug, "pizza"));
        cut.Markup.Should().Contain("Ładowanie dania...");
    }

    [Fact]
    public void DishNotFound_ShowsEmptyState()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        dishService.GetBySlugAsync("nonexistent").Returns((DishDetailDto?)null);

        var cut = RenderComponent<DishDetails>(p => p.Add(c => c.Slug, "nonexistent"));
        cut.WaitForState(() => cut.Markup.Contains("Nie znaleziono dania"));
    }

    [Fact]
    public void DishLoaded_ShowsDishDetails()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        var reviewService = Services.GetRequiredService<IReviewService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());
        reviewService.GetByDishAsync("pizza-margherita", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(CreateReviews());

        var cut = RenderComponent<DishDetails>(p => p.Add(c => c.Slug, "pizza-margherita"));
        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.Markup.Should().Contain("Pizza Margherita");
        cut.Markup.Should().Contain(29.99m.ToString("F2"));
        cut.Markup.Should().Contain("Klasyczna pizza z mozzarella");
        cut.Markup.Should().Contain("Wege");
        cut.Markup.Should().Contain("850 kcal");
    }

    [Fact]
    public void DishLoaded_ShowsRestaurantLink()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        var reviewService = Services.GetRequiredService<IReviewService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());
        reviewService.GetByDishAsync("pizza-margherita", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(CreateReviews());

        var cut = RenderComponent<DishDetails>(p => p.Add(c => c.Slug, "pizza-margherita"));
        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.Find("a[href='/restaurants/pizzeria-roma']").Should().NotBeNull();
    }

    [Fact]
    public void DishLoaded_ShowsReviews()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        var reviewService = Services.GetRequiredService<IReviewService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());
        reviewService.GetByDishAsync("pizza-margherita", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(CreateReviews());

        var cut = RenderComponent<DishDetails>(p => p.Add(c => c.Slug, "pizza-margherita"));
        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.Markup.Should().Contain("Pyszna pizza!");
    }

    [Fact]
    public void NoReviews_ShowsEmptyState()
    {
        var dishService = Services.GetRequiredService<IDishService>();
        var reviewService = Services.GetRequiredService<IReviewService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());
        reviewService.GetByDishAsync("pizza-margherita", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(new PagedResult<ReviewCardDto> { Data = [], Pagination = new PaginationInfo() });

        var cut = RenderComponent<DishDetails>(p => p.Add(c => c.Slug, "pizza-margherita"));
        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.Markup.Should().Contain("Brak recenzji");
    }

    [Fact]
    public void AuthenticatedUser_ShowsSaveButton()
    {
        SetAuthenticatedUser("testuser", "User");
        var dishService = Services.GetRequiredService<IDishService>();
        var reviewService = Services.GetRequiredService<IReviewService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());
        reviewService.GetByDishAsync("pizza-margherita", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(CreateReviews());

        var cut = RenderComponent<DishDetails>(p => p.Add(c => c.Slug, "pizza-margherita"));
        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.FindAll("button").Should().Contain(b => b.InnerHtml.Contains("fa-heart"));
    }

    [Fact]
    public void AuthenticatedUser_ShowsAddReviewLink()
    {
        SetAuthenticatedUser("testuser", "User");
        var dishService = Services.GetRequiredService<IDishService>();
        var reviewService = Services.GetRequiredService<IReviewService>();
        dishService.GetBySlugAsync("pizza-margherita").Returns(CreateDish());
        reviewService.GetByDishAsync("pizza-margherita", Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(CreateReviews());

        var cut = RenderComponent<DishDetails>(p => p.Add(c => c.Slug, "pizza-margherita"));
        cut.WaitForState(() => cut.Markup.Contains("Pizza Margherita"));

        cut.Find("a[href='/review/add?dish=pizza-margherita']").Should().NotBeNull();
    }
}
