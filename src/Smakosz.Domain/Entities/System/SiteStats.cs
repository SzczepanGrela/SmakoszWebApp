namespace Smakosz.Domain.Entities.System;

public class SiteStats
{
    public int Id { get; set; } = 1;

    public int TotalDishes { get; set; }
    public int TotalRestaurants { get; set; }
    public int TotalReviews { get; set; }
    public int TotalUsers { get; set; }
    public int TotalPhotos { get; set; }

    public int ReviewsThisWeek { get; set; }
    public int NewUsersThisMonth { get; set; }

    public double AvgDishRating { get; set; }
    public double AvgRestaurantFoodScore { get; set; }

    public string? MostPopularCuisine { get; set; }
    public string? MostActiveCity { get; set; }

    public DateTime UpdatedAt { get; set; }
}
