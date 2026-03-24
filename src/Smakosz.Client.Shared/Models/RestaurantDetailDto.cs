namespace Smakosz.Client.Models;

public class RestaurantDetailDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string RestaurantName { get; set; } = default!;
    public string? CuisineType { get; set; }
    public string? CityName { get; set; }
    public int? PriceLevel { get; set; }
    public double? AvgFoodScore { get; set; }
    public double? AvgService { get; set; }
    public double? AvgCleanliness { get; set; }
    public double? AvgAmbiance { get; set; }
    public int ReviewCount { get; set; }
    public decimal? TrendingScore { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public bool IsVerified { get; set; }
    public bool IsFavorite { get; set; }
    public List<OpeningHoursDto> OpeningHours { get; set; } = [];
    public List<MenuSectionDto> MenuSections { get; set; } = [];
}

public class OpeningHoursDto
{
    public int DayOfWeek { get; set; }
    public string OpenTime { get; set; } = default!;
    public string CloseTime { get; set; } = default!;
    public bool IsClosed { get; set; }
}

public class MenuSectionDto
{
    public string SectionName { get; set; } = default!;
    public int DisplayOrder { get; set; }
}
