using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities.System;

public class BannedIdentifier
{
    public int BanId { get; set; }
    public BannedIdentifierType Type { get; set; }
    public string Value { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public int? BannedBy { get; set; }
    public DateTime BannedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public User? BannedByUser { get; set; }
}
