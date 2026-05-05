namespace Smakosz.Domain.Entities;

public class City
{
    public int CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public DateTime CreatedAt { get; set; }
}
