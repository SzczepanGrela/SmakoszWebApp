namespace Smakosz.Client.Models;

public class RestaurantCardDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string RestaurantName { get; set; } = default!;
    public string? CuisineType { get; set; }
    public string? CityName { get; set; }
    public int? PriceLevel { get; set; }
    public double? AvgFoodScore { get; set; }
    public int ReviewCount { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }
    public bool IsFavorite { get; set; }
}
