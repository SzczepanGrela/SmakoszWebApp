namespace Smakosz.Client.Models;

public class BusinessDashboardDto
{
    public string RestaurantName { get; set; } = default!;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = default!;
    public int TotalReviews { get; set; }
    public double? AvgRating { get; set; }
    public int TotalDishes { get; set; }
    public int TotalMenuSections { get; set; }
}

public class BusinessRestaurantDto
{
    public int RestaurantId { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? ImageUrl { get; set; }
    public int? CityId { get; set; }
    public string Status { get; set; } = default!;
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
    public string Status { get; set; } = default!;
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }
}

public class CityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
}

public class NewRestaurantRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public int? CityId { get; set; }
    public int? CuisineTypeId { get; set; }
}

public class EditRequestSummaryDto
{
    public int RequestId { get; set; }
    public string ChangeType { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class BusinessMenuSectionDto
{
    public int MenuSectionId { get; set; }
    public string Name { get; set; } = default!;
    public int SortOrder { get; set; }
    public int DishCount { get; set; }
}

public class BusinessDishDto
{
    public int DishId { get; set; }
    public Guid PublicId { get; set; }
    public string DishName { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public decimal? Price { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public double? AvgRating { get; set; }
    public int ReviewCount { get; set; }
    public bool IsAvailable { get; set; }
}

public class BusinessChartDataDto
{
    public List<DailyReviewCount> ReviewTrend { get; set; } = [];
    public List<RatingDistributionItem> RatingDistribution { get; set; } = [];
    public CategoryAverages CategoryAverages { get; set; } = new();
    public List<DishRankingItem> TopDishes { get; set; } = [];
}

public class DailyReviewCount
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
}

public class RatingDistributionItem
{
    public int Rating { get; set; }
    public int Count { get; set; }
}

public class CategoryAverages
{
    public double Food { get; set; }
    public double Service { get; set; }
    public double Cleanliness { get; set; }
    public double Ambiance { get; set; }
}

public class DishRankingItem
{
    public string DishName { get; set; } = default!;
    public double AvgRating { get; set; }
    public int ReviewCount { get; set; }
}
