namespace Smakosz.Domain.Entities;

public class SavedDish
{
    public int UserId { get; set; }
    public int DishId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Dish Dish { get; set; } = null!;
}
