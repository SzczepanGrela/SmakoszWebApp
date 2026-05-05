using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class Notification : IAuditableEntity, IHasPublicId
{
    public int NotificationId { get; set; }
    public Guid PublicId { get; set; }
    public int UserId { get; set; }
    public int? ActorId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public int Priority { get; set; } = 1;
    public string? GroupKey { get; set; }
    public int Counter { get; set; } = 1;
    public bool IsRead { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool SendEmail { get; set; }
    public EmailStatus EmailStatus { get; set; } = EmailStatus.None;
    public bool SendPush { get; set; }
    public PushStatus PushStatus { get; set; } = PushStatus.None;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public User User { get; set; } = null!;
    public User? Actor { get; set; }
}
