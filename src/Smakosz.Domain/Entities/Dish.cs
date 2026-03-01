using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class Dish : IAuditableEntity, IHasPublicId
{
    public int DishId { get; set; }
    public Guid PublicId { get; set; }
    public int? RestaurantId { get; set; }
    public int? VariantId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public string? Slug { get; set; }
    public decimal? TrendingScore { get; set; }
    public bool IsVegetarian { get; set; } = true;
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; } = true;
    public bool IsLactoseFree { get; set; } = true;
    public bool IsSpicy { get; set; }
    public string? IngredientsJson { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int? Calories { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public double? AvgRating { get; set; }
    public int ReviewCount { get; set; }

    #region Generator-Only Fields
    public decimal? SecretBasePrice { get; set; }
    public string SecretCharacteristicsVector { get; set; } = "{}";
    public string? SecretPenaltyVector { get; set; }
    public double? SecretQuality { get; set; }
    public double? SecretPopularityFactor { get; set; }
    #endregion

    public Restaurant? Restaurant { get; set; }
    public DishVariant? Variant { get; set; }
    public ICollection<DishIngredient> DishIngredients { get; set; } = new List<DishIngredient>();
}
