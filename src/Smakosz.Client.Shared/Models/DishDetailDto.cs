namespace Smakosz.Client.Models;

public class DishDetailDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string DishName { get; set; } = default!;
    public decimal? Price { get; set; }
    public double? AvgRating { get; set; }
    public int ReviewCount { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }
    public string? Description { get; set; }
    public int? Calories { get; set; }
    public string? IngredientsJson { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsLactoseFree { get; set; }
    public bool IsSpicy { get; set; }
    public bool IsAvailable { get; set; }
    public decimal? TrendingScore { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }
    public string? CuisineType { get; set; }
    public string? CityName { get; set; }
    public bool IsSaved { get; set; }
    public List<TagDto> Tags { get; set; } = [];
    public List<DishIngredientDto> Ingredients { get; set; } = [];
}

public class DishIngredientDto
{
    public int IngredientId { get; set; }
    public string Name { get; set; } = default!;
    public string? IconUrl { get; set; }
    public string? IconBlurhash { get; set; }
    public bool IsAllergen { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsLactoseFree { get; set; }
}

public class TagDto
{
    public string TagName { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string? DisplayColor { get; set; }
}
