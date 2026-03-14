namespace Smakosz.Application.Features.Admin.Dtos;

public class AdminDashboardDto
{
    public int TotalUsers { get; init; }
    public int TotalRestaurants { get; init; }
    public int TotalReviews { get; init; }
    public int PendingReports { get; init; }
    public int PendingCorrections { get; init; }
    public int PendingPhotos { get; init; }
    public int PendingReviews { get; init; }
    public int OpenTickets { get; init; }
}

public class AdminUserDto
{
    public int UserId { get; init; }
    public Guid PublicId { get; init; }
    public string Username { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string Role { get; init; } = default!;
    public string Status { get; init; } = default!;
    public bool EmailVerified { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public class AdminRestaurantDto
{
    public int RestaurantId { get; init; }
    public Guid PublicId { get; init; }
    public string Name { get; init; } = default!;
    public string Slug { get; init; } = default!;
    public string Status { get; init; } = default!;
    public bool IsVerified { get; init; }
    public string? OwnerUsername { get; init; }
    public decimal AverageRating { get; init; }
    public int ReviewCount { get; init; }
}

public class AdminReportDto
{
    public int ReportId { get; init; }
    public string EntityType { get; init; } = default!;
    public int EntityId { get; init; }
    public string Reason { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? ReporterUsername { get; init; }
    public DateTime CreatedAt { get; init; }
}

public class AdminLogDto
{
    public int LogId { get; init; }
    public string Level { get; init; } = default!;
    public string Message { get; init; } = default!;
    public DateTime Timestamp { get; init; }
}

public class AdminUserDetailDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public bool IsBanned { get; set; }
    public bool Is2faEnabled { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Slug { get; set; }
    public int ReviewCount { get; set; }
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminIngredientDto
{
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public bool IsAllergen { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminCityDto
{
    public int Id { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string? Region { get; set; }
    public int RestaurantCount { get; set; }
}

public class AdminTicketDto
{
    public int TicketId { get; set; }
    public string TicketType { get; set; } = string.Empty;
    public long ReferenceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? Description { get; set; }
    public string? AssignedAdminUsername { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class PhotoModerationDto
{
    public long AssetId { get; set; }
    public Guid PublicId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    public string? UploadedByUsername { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ReviewModerationDto
{
    public int ReviewId { get; set; }
    public Guid PublicId { get; set; }
    public string? Username { get; set; }
    public string? DishName { get; set; }
    public string? RestaurantName { get; set; }
    public string? Content { get; set; }
    public int DishRating { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class EditRequestDto
{
    public int RequestId { get; set; }
    public string? RestaurantName { get; set; }
    public string? Username { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; }
}

public class SystemConfigDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSecret { get; set; }
    public bool IsPublic { get; set; }
}

public class SystemLogDto
{
    public long Id { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Context { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class JobDto
{
    public int JobId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public int Progress { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public class AiModelDto
{
    public string? ModelType { get; set; }
    public string? ModelVersion { get; set; }
    public int UsageCount { get; set; }
    public DateTime? LastUsed { get; set; }
}

public class IngredientSuggestionDto
{
    public int SuggestionId { get; set; }
    public string SuggestedName { get; set; } = string.Empty;
    public bool IsAllergen { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public bool IsGlutenFree { get; set; }
    public bool IsLactoseFree { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
    public string? Username { get; set; }
    public string? RestaurantName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class AdminTicketDetailDto
{
    public int TicketId { get; set; }
    public string TicketType { get; set; } = string.Empty;
    public long ReferenceId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string? Description { get; set; }
    public string? AssignedAdminUsername { get; set; }
    public DateTime? CreatedAt { get; set; }

    public ContactInfoDto? Contact { get; set; }
    public PhotoModerationDto? Photo { get; set; }
    public ReviewModerationDto? Review { get; set; }
    public AdminReportDto? Report { get; set; }
    public EditRequestDto? EditRequest { get; set; }
    public IngredientSuggestionDto? Suggestion { get; set; }
}

public class ContactInfoDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class AuditLogDto
{
    public long AuditLogId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}

public class SecurityLogDto
{
    public long LogId { get; set; }
    public string? EventType { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Email { get; set; }
    public int? UserId { get; set; }
    public string? Details { get; set; }
    public string? CountryCode { get; set; }
    public string? City { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class SystemNodeDto
{
    public string NodeId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? Status { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Hostname { get; set; }
    public string? GpuName { get; set; }
    public int? GpuMemoryTotal { get; set; }
    public int? GpuMemoryUsed { get; set; }
    public int? CurrentJobId { get; set; }
    public DateTime? LastHeartbeat { get; set; }
}
