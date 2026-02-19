using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities.System;

public class SecurityLog
{
    public long LogId { get; set; }
    public SecurityEventType? EventType { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Email { get; set; }
    public int? UserId { get; set; }
    public string? Details { get; set; }
    public string? CountryCode { get; set; }
    public string? City { get; set; }
    public DateTime? CreatedAt { get; set; }

    public User? User { get; set; }
}
