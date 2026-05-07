namespace Smakosz.Domain.Entities;

public class UserNotificationSettings
{
    public int UserId { get; set; }
    public bool PushLike { get; set; } = true;
    public bool PushFollow { get; set; } = true;
    public bool PushSystem { get; set; } = true;
    public bool EmailSecurity { get; set; } = true;
    public bool PushSecurity { get; set; } = false;
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
