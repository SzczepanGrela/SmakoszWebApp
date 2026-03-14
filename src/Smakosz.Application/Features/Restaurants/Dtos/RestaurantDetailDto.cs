namespace Smakosz.Application.Features.Restaurants.Dtos;

public class RestaurantDetailDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string RestaurantName { get; init; } = default!;
    public string? CuisineType { get; init; }
    public string? CityName { get; init; }
    public int? PriceLevel { get; init; }
    public double? AvgFoodScore { get; init; }
    public double? AvgService { get; init; }
    public double? AvgCleanliness { get; init; }
    public double? AvgAmbiance { get; init; }
    public int ReviewCount { get; init; }
    public decimal? TrendingScore { get; init; }
    public string? ImageUrl { get; init; }
    public string? ImageBlurhash { get; init; }
    public string? Description { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public bool IsVerified { get; init; }
    public bool IsFavorite { get; init; }
    public required List<OpeningHoursDto> OpeningHours { get; init; }
    public required List<MenuSectionDto> MenuSections { get; init; }
}

public class OpeningHoursDto
{
    public int DayOfWeek { get; init; }
    public TimeOnly OpenTime { get; init; }
    public TimeOnly CloseTime { get; init; }
    public bool IsClosed { get; init; }
}

public class MenuSectionDto
{
    public string SectionName { get; init; } = default!;
    public int DisplayOrder { get; init; }
}
