namespace Smakosz.Client.Models;

public class BusinessDashboardDto
{
    public string RestaurantName { get; set; } = default!;
    public string? ImageUrl { get; set; }
    public string Status { get; set; } = default!;
    public int TotalReviews { get; set; }
    public double? AvgRating { get; set; }
    public int TotalDishes { get; set; }
    public int ViewsThisMonth { get; set; }
    public List<BusinessStatsDto> WeeklyStats { get; set; } = [];
}

public class BusinessRestaurantDto
{
    public Guid PublicId { get; set; }
    public string RestaurantName { get; set; } = default!;
    public string? Description { get; set; }
    public string? CuisineType { get; set; }
    public int? PriceLevel { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? ImageUrl { get; set; }
    public int? CityId { get; set; }
    public string? CityName { get; set; }
}

public class BusinessStatsDto
{
    public string Date { get; set; } = default!;
    public int Views { get; set; }
    public int Reviews { get; set; }
}

public class RegistrationStatusDto
{
    public string Status { get; set; } = default!;
    public string? Message { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public class CityDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
}

public class EditRequestSummaryDto
{
    public Guid Id { get; set; }
    public string FieldChanged { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
