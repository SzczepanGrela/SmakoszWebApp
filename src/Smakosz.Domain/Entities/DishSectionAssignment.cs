namespace Smakosz.Domain.Entities;

public class DishSectionAssignment
{
    public int DishId { get; set; }
    public int SectionId { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Dish Dish { get; set; } = null!;
    public MenuSection Section { get; set; } = null!;
}
