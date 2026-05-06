using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class RestaurantTheme : IHasPublicId, IAuditableEntity
{
    public int ThemeId { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? Icon { get; set; }
    public int CuisineTypeId { get; set; }
    public double Weight { get; set; }
    public string? Prompt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public CuisineType Cuisine { get; set; } = null!;
}
