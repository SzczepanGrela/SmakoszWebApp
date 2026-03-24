namespace Smakosz.Client.Models;

public class RecommendationsDto
{
    public List<RecommendationItemDto> Items { get; set; } = [];
    public string? GeneratedAt { get; set; }
}

public class RecommendationItemDto
{
    public DishCardDto Dish { get; set; } = new();
    public double Score { get; set; }
    public string Reason { get; set; } = default!;
}
