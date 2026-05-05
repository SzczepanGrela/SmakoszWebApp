namespace Smakosz.Domain.Entities;

public class FavoriteRestaurant
{
    public int UserId { get; set; }
    public int RestaurantId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
    public Restaurant Restaurant { get; set; } = null!;
}
