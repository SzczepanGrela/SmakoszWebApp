namespace Smakosz.Domain.Entities;

public class MenuSection
{
    public int SectionId { get; set; }
    public int RestaurantId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTime? CreatedAt { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
}
