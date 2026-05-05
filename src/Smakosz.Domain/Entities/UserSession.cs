namespace Smakosz.Domain.Entities;

public class UserSession
{
    public long UserSessionId { get; set; }
    public int UserId { get; set; }
    public string RefreshTokenHash { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? IpAddress { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public bool IsRevoked { get; set; }
    public bool IsRememberMe { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
