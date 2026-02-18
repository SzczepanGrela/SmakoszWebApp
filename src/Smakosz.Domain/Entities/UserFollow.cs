namespace Smakosz.Domain.Entities;

public class UserFollow
{
    public int FollowerId { get; set; }
    public int FollowedId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Follower { get; set; } = null!;
    public User Followed { get; set; } = null!;
}
