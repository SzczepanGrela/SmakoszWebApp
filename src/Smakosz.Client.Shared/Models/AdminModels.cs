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

public class AdminUserDetailDto
{
    public int UserId { get; set; }
    public Guid PublicId { get; set; }
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public string Status { get; set; } = default!;
    public bool EmailVerified { get; set; }
    public bool IsBanned { get; set; }
    public bool IsActive { get; set; }
    public bool Is2faEnabled { get; set; }
    public string? AvatarUrl { get; set; }
    public string? AvatarBlurhash { get; set; }
    public string? Slug { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int ReviewCount { get; set; }
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
    public int PhotoCount { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
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

public class BulkModeratePhotosResultDto
{
    public List<Guid> Success { get; set; } = new();
    public List<BulkModerateFailureDto> Failed { get; set; } = new();
}

public class BulkModerateFailureDto
{
    public Guid PublicId { get; set; }
    public string ErrorCode { get; set; } = default!;
    public string Message { get; set; } = default!;
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
    public string? IconUrl { get; set; }
    public string? IconBlurhash { get; set; }
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

public class AdminCuisineTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Icon { get; set; }
    public bool IsActive { get; set; } = true;
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

public class AdminDishDto
{
    public int DishId { get; set; }
    public Guid PublicId { get; set; }
    public string DishName { get; set; } = default!;
    public decimal? Price { get; set; }
    public bool IsAvailable { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }
    public string ModerationStatus { get; set; } = default!;
    public double? AvgRating { get; set; }
    public int ReviewCount { get; set; }
    public decimal? TrendingScore { get; set; }
    public string? Slug { get; set; }
    public int? RestaurantId { get; set; }
    public string? RestaurantName { get; set; }
    public List<string> Ingredients { get; set; } = new();
    public List<AdminTagDto> Tags { get; set; } = new();
    public string? CategoryTagName { get; set; }
    public string? CategoryColor { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminJobDto
{
    public int JobId { get; set; }
    public string Type { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int Priority { get; set; }
    public int Progress { get; set; }
    public string? ProgressMessage { get; set; }
    public string? WorkerNode { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorLog { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
}

public class AdminNcfStatusDto
{
    public bool NcfAvailable { get; set; }
    public string? FallbackReason { get; set; }
    public string LoadedVersion { get; set; } = string.Empty;
    public int MappedUsersCount { get; set; }
    public int CachePopulatedCount { get; set; }
    public double CachePopulatedPercent { get; set; }
    public AdminNcfTrainingSummaryDto? LastTraining { get; set; }
    public AdminNcfRegenSummaryDto? LastCacheRegen { get; set; }
    public List<AdminNcfTrainingSummaryDto> RecentTrainings { get; set; } = [];
}

public class AdminNcfTrainingSummaryDto
{
    public int JobId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public double? DurationSeconds { get; set; }
    public string? WorkerNode { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AdminNcfRegenSummaryDto
{
    public DateTime? LastRow { get; set; }
    public DateTime? FirstRowInBatch { get; set; }
    public double? ApproxDurationSeconds { get; set; }
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
    public string? CuisineTypeName { get; set; }
    public string? CuisineTypeIcon { get; set; }
    public decimal AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

public class AdminRestaurantDetailDto
{
    // Core
    public int RestaurantId { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = default!;
    public string? Slug { get; set; }
    public string? Description { get; set; }
    public int? CuisineTypeId { get; set; }
    public string? CuisineType { get; set; }
    public int? PriceLevel { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }

    // Contact & location
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public int? CityId { get; set; }
    public string? CityName { get; set; }

    // Owner
    public int? OwnerId { get; set; }
    public Guid? OwnerPublicId { get; set; }
    public string? OwnerUsername { get; set; }
    public string? OwnerEmail { get; set; }

    // Status
    public string Status { get; set; } = default!;
    public bool IsVerified { get; set; }
    public string ModerationStatus { get; set; } = default!;
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedByUsername { get; set; }
    public int Version { get; set; }

    // Metrics
    public double? AvgFoodScore { get; set; }
    public double? AvgServiceScore { get; set; }
    public double? AvgCleanlinessScore { get; set; }
    public double? AvgAmbianceScore { get; set; }
    public decimal? TrendingScore { get; set; }
    public int ReviewCount { get; set; }

    // Counters
    public int PendingEditRequestsCount { get; set; }
    public int PendingPhotosCount { get; set; }
    public int ApprovedPhotosCount { get; set; }
    public int MenuSectionsCount { get; set; }
    public int MenuItemsCount { get; set; }

    // Nested
    public List<AdminRestaurantOpeningHoursDto> OpeningHours { get; set; } = new();
    public List<AdminRestaurantReviewSummaryDto> RecentReviews { get; set; } = new();

    // Metadata
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class AdminCreateRestaurantDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int CityId { get; set; }
    public int CuisineTypeId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public int? OwnerId { get; set; }
    public int? TicketId { get; set; }
}

public class AdminRestaurantUpdateDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? CuisineTypeId { get; set; }
    public int? PriceLevel { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public int? CityId { get; set; }
    public int ExpectedVersion { get; set; }
}

public class AdminBannedIdentifierDto
{
    public int BanId { get; set; }
    public string Type { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Reason { get; set; }
    public string? BannedByUsername { get; set; }
    public DateTime? BannedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
}

public class AdminForbiddenWordDto
{
    public int WordId { get; set; }
    public string Word { get; set; } = default!;
    public string Category { get; set; } = default!;
    public bool IsRegex { get; set; }
    public string? AddedByUsername { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminRejectionReasonDto
{
    public string ReasonCode { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string AdminLabel { get; set; } = default!;
    public string UserMessageTemplate { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminModerationLogDto
{
    public long LogId { get; set; }
    public string EntityType { get; set; } = default!;
    public int EntityId { get; set; }
    public string Actor { get; set; } = default!;
    public string Verdict { get; set; } = default!;
    public List<string> ReasonCodes { get; set; } = new();
    public string? AdminNote { get; set; }
    public int? ProcessedBy { get; set; }
    public string? ProcessedByUsername { get; set; }
    public string? AiScores { get; set; }
    public string? ContentFullText { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminRestaurantOpeningHoursDto
{
    public int DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public bool IsClosed { get; set; }
}

public class AdminRestaurantReviewSummaryDto
{
    public int ReviewId { get; set; }
    public Guid PublicId { get; set; }
    public string? Username { get; set; }
    public string? DishName { get; set; }
    public int DishRating { get; set; }
    public string? ContentPreview { get; set; }
    public string ModerationStatus { get; set; } = default!;
    public DateTime? CreatedAt { get; set; }
}

public class AdminHeroImageDto
{
    public Guid PublicId { get; set; }
    public string Url { get; set; } = default!;
    public string? Blurhash { get; set; }
    public string? CreditText { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminIngredientIconDto
{
    public string IconUrl { get; set; } = default!;
    public string? IconBlurhash { get; set; }
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

    public int? RequesterId { get; set; }
    public string? RequesterUsername { get; set; }
    public string? RequesterEmail { get; set; }
    public int? RestaurantId { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }

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
    public string ChangedByUsername { get; set; } = default!;
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

public class AdminEmailLogDto
{
    public long LogId { get; set; }
    public string? Type { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
}

public class AdminAiLogDto
{
    public long LogId { get; set; }
    public string? ModelType { get; set; }
    public string? ModelName { get; set; }
    public string? ModelVersion { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string? InputSummary { get; set; }
    public string? Scores { get; set; }
    public string? Verdict { get; set; }
    public int? ProcessingTimeMs { get; set; }
    public bool Fallback { get; set; }
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

public class AdminSystemNodesResponseDto
{
    public List<AdminSystemNodeDto> Nodes { get; set; } = new();
    public int StaleThresholdDays { get; set; } = 7;
}

public class GpuWakeResultDto
{
    public string Status { get; set; } = default!;
    public string? Message { get; set; }
}

public class AdminUserActionLogDto
{
    public long LogId { get; set; }
    public string ActionType { get; set; } = default!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public string? ActorUsername { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class AdminUserFollowerDto
{
    public Guid PublicId { get; set; }
    public string Username { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public DateTime FollowedAt { get; set; }
}

public class AdminUserRestaurantClaimDto
{
    public int TicketId { get; set; }
    public int RestaurantId { get; set; }
    public string RestaurantName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime? CreatedAt { get; set; }
}

public class AdminUserReviewDto
{
    public Guid PublicId { get; set; }
    public Guid DishPublicId { get; set; }
    public string DishName { get; set; } = default!;
    public string RestaurantName { get; set; } = default!;
    public int DishRating { get; set; }
    public string ModerationStatus { get; set; } = default!;
    public DateTime? CreatedAt { get; set; }
}

public record TicketSummaryDto(string TicketType, int OpenCount, DateTime? OldestOpenAt);
