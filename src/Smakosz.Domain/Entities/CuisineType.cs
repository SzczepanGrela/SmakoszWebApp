namespace Smakosz.Domain.Entities;

public class CuisineType
{
    public int CuisineTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Icon { get; set; }
}
