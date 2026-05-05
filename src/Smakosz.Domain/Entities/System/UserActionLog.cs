namespace Smakosz.Domain.Entities.System;

public class UserActionLog
{
    public long ActionLogId { get; set; }
    public int UserId { get; set; }
    public int? ActorUserId { get; set; }
    public string ActionType { get; set; } = default!;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = default!;
    public User? Actor { get; set; }
}
