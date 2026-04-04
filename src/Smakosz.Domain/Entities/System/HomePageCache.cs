namespace Smakosz.Domain.Entities.System;

public class HomePageCache
{
    public int Id { get; set; } = 1;

    public string? TrendingRestaurantsJson { get; set; }
    public string? TrendingDishesJson { get; set; }
    public string? TopRatedDishesJson { get; set; }
    public string? RecentReviewsJson { get; set; }
    public string? PopularCategoriesJson { get; set; }
    public string? HeroImageJson { get; set; }

    public DateTime UpdatedAt { get; set; }
}
