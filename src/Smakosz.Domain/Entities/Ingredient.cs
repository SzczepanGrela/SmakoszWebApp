namespace Smakosz.Domain.Entities;

public class Ingredient
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public string? IconBlurhash { get; set; }
    public bool IsAllergen { get; set; }
    public bool IsVegetarian { get; set; } = true;
    public bool IsVegan { get; set; } = true;
    public bool IsGlutenFree { get; set; } = true;
    public bool IsLactoseFree { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
