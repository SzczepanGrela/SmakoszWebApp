namespace Smakosz.Application.Features.Business.Dtos;

public class BusinessDashboardDto
{
    public string RestaurantName { get; init; } = default!;
    public string? ImageUrl { get; init; }
    public string Status { get; init; } = default!;
    public double? AvgRating { get; init; }
    public int TotalReviews { get; init; }
    public int TotalDishes { get; init; }
    public int TotalMenuSections { get; init; }
}

public class BusinessRestaurantDto
{
    public int RestaurantId { get; init; }
    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string? Description { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? ImageUrl { get; init; }
    public int? CityId { get; init; }
    public string Status { get; init; } = default!;
}

public class BusinessMenuSectionDto
{
    public int MenuSectionId { get; init; }
    public string Name { get; init; } = default!;
    public int SortOrder { get; init; }
    public int DishCount { get; init; }
}

public class BusinessDishDto
{
    public int DishId { get; init; }
    public Guid PublicId { get; init; }
    public string DishName { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public decimal? Price { get; init; }
    public string? Description { get; init; }
    public string? ImageUrl { get; init; }
    public double? AvgRating { get; init; }
    public int ReviewCount { get; init; }
    public bool IsAvailable { get; init; }
}

public class OpeningHoursDto
{
    public int DayOfWeek { get; init; }
    public TimeOnly OpenTime { get; init; }
    public TimeOnly CloseTime { get; init; }
    public bool IsClosed { get; init; }
}

public class BusinessReviewDto
{
    public int ReviewId { get; set; }
    public string? Username { get; set; }
    public string? DishName { get; set; }
    public int DishRating { get; set; }
    public int ServiceRating { get; set; }
    public string? Content { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class BusinessStatsDto
{
    public int TotalReviews { get; set; }
    public double? AverageRating { get; set; }
    public int ReviewsThisMonth { get; set; }
    public int ReviewsLastMonth { get; set; }
}

public class RegistrationStatusDto
{
    public bool HasRestaurant { get; set; }
    public string? Status { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }
}

public class OpeningHoursItemDto
{
    public int DayOfWeek { get; set; }
    public string OpenTime { get; set; } = string.Empty;
    public string CloseTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}
