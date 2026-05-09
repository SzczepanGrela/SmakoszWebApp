namespace Smakosz.Client.Models;

public class RecommendationsDto
{
    public bool NcfAvailable { get; set; }
    public bool IsNewcomer { get; set; }
    public string? FallbackReason { get; set; }
    public List<RecommendedDishDto> Trending { get; set; } = [];
    public List<RecommendedDishDto> Personalized { get; set; } = [];
}

public class RecommendedDishDto
{
    public int DishId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ImageUrl { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }
    public string Source { get; set; } = "trending";
    public decimal? Score { get; set; }
}
