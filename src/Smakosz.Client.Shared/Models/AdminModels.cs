namespace Smakosz.Client.Models;

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalRestaurants { get; set; }
    public int TotalReviews { get; set; }
    public int PendingReports { get; set; }
    public int PendingCorrections { get; set; }
    public int PendingPhotos { get; set; }
    public int PendingReviews { get; set; }
    public int OpenTickets { get; set; }
}

public class AdminUserDto
{
    public int UserId { get; set; }
    public Guid PublicId { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public string Status { get; set; } = default!;
    public bool EmailVerified { get; set; }
    public bool Is2faEnabled { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminTicketDto
{
    public int TicketId { get; set; }
    public string TicketType { get; set; } = default!;
    public long ReferenceId { get; set; }
    public string Status { get; set; } = default!;
    public int Priority { get; set; }
    public string? Description { get; set; }
    public string? AssignedAdminUsername { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminPhotoDto
{
    public Guid PublicId { get; set; }
    public string Url { get; set; } = default!;
    public string? Status { get; set; }
    public string? UploadedByUsername { get; set; }
    public string? EntityType { get; set; }
    public string? EntityName { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid Id => PublicId;
    public string ImageUrl => Url;
    public string UploadedBy => UploadedByUsername ?? "Nieznany";
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
    public int ReportId { get; set; }
    public string Reason { get; set; } = default!;
    public string ReporterUsername { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public int EntityId { get; set; }
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
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = default!;
    public bool IsAllergen { get; set; }
    public bool IsVegetarian { get; set; }
    public bool IsVegan { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminCityDto
{
    public int Id { get; set; }
    public string CityName { get; set; } = default!;
    public string? Region { get; set; }
    public int RestaurantCount { get; set; }
}

public class AdminTagDto
{
    public int TagId { get; set; }
    public string TagName { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string TargetEntity { get; set; } = default!;
    public string? DisplayColor { get; set; }
    public int UsageCount { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminAiModelDto
{
    public string ModelType { get; set; } = default!;
    public string ModelVersion { get; set; } = default!;
    public int UsageCount { get; set; }
    public DateTime? LastUsed { get; set; }
}

public class AdminJobDto
{
    public int JobId { get; set; }
    public string Type { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int Priority { get; set; }
    public int Progress { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public class CreateJobRequest
{
    public string Type { get; set; } = default!;
    public int Priority { get; set; }
    public string? Payload { get; set; }
    public string? EntityId { get; set; }
    public string? EntityType { get; set; }
}

public class AdminSystemConfigDto
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsSecret { get; set; }
    public bool IsPublic { get; set; }
}

public class AdminLogEntryDto
{
    public long Id { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string Level { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? Source { get; set; }
    public string? Context { get; set; }
}

public class AdminRestaurantDto
{
    public int RestaurantId { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string Status { get; set; } = default!;
    public bool IsVerified { get; set; }
    public string? OwnerUsername { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class AdminHeroImageDto
{
    public Guid PublicId { get; set; }
    public string Url { get; set; } = default!;
    public string? Blurhash { get; set; }
    public string? CreditText { get; set; }
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

public class AdminTicketDetailDto
{
    public int TicketId { get; set; }
    public string TicketType { get; set; } = default!;
    public long ReferenceId { get; set; }
    public string Status { get; set; } = default!;
    public int Priority { get; set; }
    public string? Description { get; set; }
    public string? AssignedAdminUsername { get; set; }
    public DateTime? CreatedAt { get; set; }

    public ContactInfoDto? Contact { get; set; }
    public AdminPhotoModerationDto? Photo { get; set; }
    public AdminReviewModerationDto? Review { get; set; }
    public AdminReportDto? Report { get; set; }
    public AdminEditRequestModerationDto? EditRequest { get; set; }
    public AdminIngredientSuggestionDto? Suggestion { get; set; }
}

public class ContactInfoDto
{
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Subject { get; set; } = default!;
    public string Message { get; set; } = default!;
}

public class AdminPhotoModerationDto
{
    public long AssetId { get; set; }
    public Guid PublicId { get; set; }
    public string Url { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public int EntityId { get; set; }
    public string? UploadedByUsername { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminReviewModerationDto
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

public class AdminEditRequestModerationDto
{
    public int RequestId { get; set; }
    public string? RestaurantName { get; set; }
    public string? Username { get; set; }
    public string ChangeType { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Payload { get; set; } = default!;
    public DateTime? CreatedAt { get; set; }
}

public class AdminAuditLogDto
{
    public long AuditLogId { get; set; }
    public string TableName { get; set; } = default!;
    public int RecordId { get; set; }
    public string Operation { get; set; } = default!;
    public string ChangedBy { get; set; } = default!;
    public DateTime ChangedAt { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}

public class AdminSecurityLogDto
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

public class AdminSystemNodeDto
{
    public string NodeId { get; set; } = default!;
    public string? IpAddress { get; set; }
    public string? Status { get; set; }
    public string NodeType { get; set; } = default!;
    public string? Role { get; set; }
    public string? Hostname { get; set; }
    public string? GpuName { get; set; }
    public int? GpuMemoryTotal { get; set; }
    public int? GpuMemoryUsed { get; set; }
    public int? CurrentJobId { get; set; }
    public DateTime? LastHeartbeat { get; set; }
}
