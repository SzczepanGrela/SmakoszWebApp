namespace Smakosz.Client.Models;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalRestaurants { get; set; }
    public int TotalDishes { get; set; }
    public int TotalReviews { get; set; }
    public int PendingTickets { get; set; }
    public int PendingPhotos { get; set; }
    public int PendingReviews { get; set; }
    public int PendingEditRequests { get; set; }
    public List<AdminActivityDto> RecentActivity { get; set; } = [];
}

public class AdminActivityDto
{
    public string Type { get; set; } = default!;
    public string Description { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public class AdminUserDto
{
    public Guid PublicId { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public bool IsActive { get; set; }
    public bool IsBanned { get; set; }
    public bool EmailVerified { get; set; }
    public int ReviewCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminTicketDto
{
    public int Id { get; set; }
    public string Subject { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Priority { get; set; } = default!;
    public string? AssignedTo { get; set; }
    public string CreatedBy { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class AdminPhotoDto
{
    public Guid Id { get; set; }
    public string ImageUrl { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string UploadedBy { get; set; } = default!;
    public string? EntityType { get; set; }
    public string? EntityName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminReviewDto
{
    public Guid PublicId { get; set; }
    public string Content { get; set; } = default!;
    public int ContentStatus { get; set; }
    public string AuthorUsername { get; set; } = default!;
    public string DishName { get; set; } = default!;
    public string RestaurantName { get; set; } = default!;
    public int DishRating { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ReportCount { get; set; }
}

public class AdminReportDto
{
    public int Id { get; set; }
    public string Reason { get; set; } = default!;
    public string? Description { get; set; }
    public string ReportedBy { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public class AdminEditRequestDto
{
    public int Id { get; set; }
    public string RestaurantName { get; set; } = default!;
    public string FieldChanged { get; set; } = default!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string Status { get; set; } = default!;
    public string RequestedBy { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}

public class AdminIngredientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Category { get; set; }
    public bool IsAllergen { get; set; }
    public int UsageCount { get; set; }
}

public class AdminCityDto
{
    public int Id { get; set; }
    public string CityName { get; set; } = default!;
    public string? Region { get; set; }
    public int RestaurantCount { get; set; }
}

public class AdminAiModelDto
{
    public string ModelName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Version { get; set; }
    public DateTime? LastRun { get; set; }
    public int ProcessedCount { get; set; }
    public double? Accuracy { get; set; }
}

public class AdminJobDto
{
    public int Id { get; set; }
    public string JobName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Schedule { get; set; }
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

public class AdminSystemConfigDto
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
    public string? Category { get; set; }
}

public class AdminLogEntryDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? Source { get; set; }
    public string? Exception { get; set; }
}

public class AdminHeroImageDto
{
    public Guid PublicId { get; set; }
    public string Url { get; set; } = default!;
    public string? Blurhash { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminIngredientSuggestionDto
{
    public int SuggestionId { get; set; }
    public string SuggestedName { get; set; } = default!;
    public bool IsAllergen { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsLactoseFree { get; set; }
    public string Status { get; set; } = default!;
    public string? AdminNote { get; set; }
    public string? Username { get; set; }
    public string? RestaurantName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
