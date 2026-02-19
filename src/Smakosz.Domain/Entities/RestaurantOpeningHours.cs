namespace Smakosz.Domain.Entities;

public class RestaurantOpeningHours
{
    public int HoursId { get; set; }
    public int RestaurantId { get; set; }
    public int DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsClosed { get; set; }

    public Restaurant Restaurant { get; set; } = null!;
}
