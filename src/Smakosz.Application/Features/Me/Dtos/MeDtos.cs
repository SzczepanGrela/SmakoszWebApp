namespace Smakosz.Application.Features.Me.Dtos;

public class MyProfileDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string? AvatarUrl { get; init; }
    public string? AvatarBlurhash { get; init; }
    public string Role { get; init; } = default!;
    public bool EmailVerified { get; init; }
    public bool Is2faEnabled { get; init; }
    public int ReviewCount { get; init; }
    public int FollowersCount { get; init; }
    public int FollowingCount { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public class SessionDto
{
    public long SessionId { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public bool IsCurrent { get; init; }
}

public class NotificationDto
{
    public int NotificationId { get; init; }
    public string Type { get; init; } = default!;
    public string Message { get; init; } = default!;
    public bool IsRead { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public class NotificationSettingsDto
{
    public bool PushLike { get; set; }
    public bool PushFollow { get; set; }
    public bool PushSystem { get; set; }
    public bool EmailSecurity { get; set; } = true;
    public bool PushSecurity { get; set; }
}

public class SavedDishDto
{
    public int DishId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ImageUrl { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }
    public decimal? Price { get; set; }
    public DateTime? SavedAt { get; set; }
}

public class FavoriteRestaurantDto
{
    public int RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? ImageUrl { get; set; }
    public string? CuisineType { get; set; }
    public double? AvgRating { get; set; }
    public DateTime? FavoritedAt { get; set; }
}

public class FollowUserDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MyReviewDto
{
    public int ReviewId { get; set; }
    public string? DishName { get; set; }
    public string? DishSlug { get; set; }
    public string? RestaurantName { get; set; }
    public string? RestaurantSlug { get; set; }
    public int DishRating { get; set; }
    public string? Content { get; set; }
    public DateTime? CreatedAt { get; set; }
}
