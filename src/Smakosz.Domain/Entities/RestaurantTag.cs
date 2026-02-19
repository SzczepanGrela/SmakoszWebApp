namespace Smakosz.Domain.Entities;

public class RestaurantTag
{
    public int RestaurantId { get; set; }
    public int TagId { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
