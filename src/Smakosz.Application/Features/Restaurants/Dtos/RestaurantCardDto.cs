namespace Smakosz.Application.Features.Restaurants.Dtos;

public class RestaurantCardDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string RestaurantName { get; init; } = default!;
    public string? CuisineType { get; init; }
    public string? CityName { get; init; }
    public int? PriceLevel { get; init; }
    public double? AvgFoodScore { get; init; }
    public int ReviewCount { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageBlurhash { get; init; }
    public bool IsFavorite { get; init; }
}
