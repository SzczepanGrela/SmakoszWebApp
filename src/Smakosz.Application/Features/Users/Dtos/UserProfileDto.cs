namespace Smakosz.Application.Features.Users.Dtos;

public class PublicUserProfileDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string? AvatarUrl { get; init; }
    public string? AvatarBlurhash { get; init; }
    public int ReviewCount { get; init; }
    public int FollowersCount { get; init; }
    public int FollowingCount { get; init; }
    public DateTime? CreatedAt { get; init; }
    public bool IsFollowedByCurrentUser { get; init; }
}

public class UserListItemDto
{
    public Guid PublicId { get; init; }
    public string Slug { get; init; } = default!;
    public string Username { get; init; } = default!;
    public string? AvatarUrl { get; init; }
    public int ReviewCount { get; init; }
    public bool IsFollowing { get; init; }
    public DateTime? FollowedAt { get; init; }
}
