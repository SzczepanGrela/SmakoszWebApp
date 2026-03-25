namespace Smakosz.Application.Features.Dishes.Dtos;

public class DishDetailDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string DishName { get; init; } = default!;
    public decimal? Price { get; init; }
    public double? AvgRating { get; init; }
    public int ReviewCount { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageBlurhash { get; init; }
    public string? Description { get; init; }
    public int? Calories { get; init; }
    public string? IngredientsJson { get; init; }
    public bool IsVegetarian { get; init; }
    public bool IsVegan { get; init; }
    public bool IsGlutenFree { get; init; }
    public bool IsLactoseFree { get; init; }
    public bool IsSpicy { get; init; }
    public bool IsAvailable { get; init; }
    public decimal? TrendingScore { get; init; }
    public string? RestaurantName { get; init; }
    public string? RestaurantSlug { get; init; }
    public string? CuisineType { get; init; }
    public string? CityName { get; init; }
    public bool IsSaved { get; init; }
    public List<TagDto> Tags { get; init; } = [];
    public List<DishIngredientDto> Ingredients { get; init; } = [];
}

public class DishIngredientDto
{
    public int IngredientId { get; init; }
    public string Name { get; init; } = default!;
    public string? IconUrl { get; init; }
    public string? IconBlurhash { get; init; }
    public bool IsAllergen { get; init; }
    public bool IsVegetarian { get; init; }
    public bool IsVegan { get; init; }
    public bool IsGlutenFree { get; init; }
    public bool IsLactoseFree { get; init; }
}

public class TagDto
{
    public string TagName { get; init; } = default!;
    public string Category { get; init; } = default!;
    public string? DisplayColor { get; init; }
}
