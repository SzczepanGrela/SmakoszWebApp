namespace Smakosz.Client.Models;

public class MyProfileDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? City { get; set; }
    public int ReviewCount { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public int SavedDishCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserPublicProfileDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public string? City { get; set; }
    public int ReviewCount { get; set; }
    public int FollowerCount { get; set; }
    public int FollowingCount { get; set; }
    public bool IsFollowing { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SessionDto
{
    public Guid SessionId { get; set; }
    public string Device { get; set; } = default!;
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCurrent { get; set; }
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationSettingsDto
{
    public bool EmailNotifications { get; set; }
    public bool PushNotifications { get; set; }
    public bool ReviewReplies { get; set; }
    public bool NewFollowers { get; set; }
    public bool Recommendations { get; set; }
}
