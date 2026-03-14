namespace Smakosz.Domain.Entities;

public class PushSubscription
{
    public int PushSubscriptionId { get; set; }
    public int UserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
