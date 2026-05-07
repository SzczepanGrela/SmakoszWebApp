namespace Smakosz.Client.Models;

public class MyProfileDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? AvatarBlurhash { get; set; }
    public string Role { get; set; } = default!;
    public bool EmailVerified { get; set; }
    public bool Is2faEnabled { get; set; }
    public int ReviewCount { get; set; }
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class UserPublicProfileDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? AvatarBlurhash { get; set; }
    public int ReviewCount { get; set; }
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
    public bool IsFollowedByCurrentUser { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class SessionDto
{
    public long SessionId { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsCurrent { get; set; }
}

public class NotificationDto
{
    public int NotificationId { get; set; }
    public Guid PublicId { get; set; }
    public string Type { get; set; } = default!;
    public string Message { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class NotificationSettingsDto
{
    public bool PushLike { get; set; }
    public bool PushFollow { get; set; }
    public bool PushSystem { get; set; }
    public bool EmailSecurity { get; set; } = true;
    public bool PushSecurity { get; set; }
}
