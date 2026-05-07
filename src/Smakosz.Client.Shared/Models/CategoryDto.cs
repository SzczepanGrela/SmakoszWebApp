namespace Smakosz.Client.Models;

public class CategoryDto
{
    public string Name { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public int RestaurantCount { get; init; }
}
