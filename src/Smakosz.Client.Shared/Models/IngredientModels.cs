namespace Smakosz.Client.Models;

public class IngredientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? IconUrl { get; set; }
    public bool IsAllergen { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsLactoseFree { get; set; }
}
