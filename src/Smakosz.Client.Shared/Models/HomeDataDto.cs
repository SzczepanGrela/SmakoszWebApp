namespace Smakosz.Client.Models;

public class HomeDataDto
{
    public StatsDto Stats { get; set; } = new();
    public List<RestaurantCardDto> TrendingRestaurants { get; set; } = [];
    public List<DishCardDto> TrendingDishes { get; set; } = [];
    public List<DishCardDto> TopRatedDishes { get; set; } = [];
    public List<ReviewCardDto> RecentReviews { get; set; } = [];
    public List<PopularCategoryDto> PopularCategories { get; set; } = [];
    public HeroImageDto? HeroImage { get; set; }
    public List<RestaurantCardDto> NewestRestaurants { get; set; } = [];
    public List<DishCardDto> MostReviewedDishes { get; set; } = [];
}

public class PopularCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? Icon { get; set; }
}

public class HeroImageDto
{
    public string Url { get; set; } = string.Empty;
    public string? Blurhash { get; set; }
    public string? CreditText { get; set; }
}

public class StatsDto
{
    public int TotalDishes { get; set; }
    public int TotalRestaurants { get; set; }
    public int TotalReviews { get; set; }
}
