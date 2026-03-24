namespace Smakosz.Client.Models;

public class DishCardDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string DishName { get; set; } = default!;
    public decimal? Price { get; set; }
    public double? AvgRating { get; set; }
    public int ReviewCount { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsSaved { get; set; }
}
