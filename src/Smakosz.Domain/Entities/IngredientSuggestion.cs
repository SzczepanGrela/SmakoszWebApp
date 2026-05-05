using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class IngredientSuggestion : IVersioned
{
    public int SuggestionId { get; set; }
    public int? UserId { get; set; }
    public int RestaurantId { get; set; }
    public string SuggestedName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? IconBlurhash { get; set; }
    public bool IsAllergen { get; set; }
    public bool IsVegetarian { get; set; } = true;
    public bool IsVegan { get; set; } = true;
    public bool IsGlutenFree { get; set; } = true;
    public bool IsLactoseFree { get; set; } = true;
    public IngredientSuggestionStatus Status { get; set; } = IngredientSuggestionStatus.Pending;
    public string? AdminNote { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? MergedIngredientId { get; set; }
    public int Version { get; set; } = 1;

    public User? User { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
    public User? ReviewedByAdmin { get; set; }
    public Ingredient? MergedIngredient { get; set; }
}
