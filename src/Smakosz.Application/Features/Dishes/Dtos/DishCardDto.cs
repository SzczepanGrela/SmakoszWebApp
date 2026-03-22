namespace Smakosz.Application.Features.Dishes.Dtos;

public class DishCardDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string DishName { get; init; } = default!;
    public decimal? Price { get; init; }
    public double? AvgRating { get; init; }
    public int ReviewCount { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageBlurhash { get; init; }
    public string? RestaurantName { get; init; }
    public string? RestaurantSlug { get; init; }
    public bool IsVegetarian { get; init; }
    public bool IsVegan { get; init; }
    public bool IsGlutenFree { get; init; }
    public bool IsSaved { get; init; }
}
