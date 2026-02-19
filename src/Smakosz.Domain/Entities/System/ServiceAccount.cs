namespace Smakosz.Domain.Entities.System;

public class ServiceAccount
{
    public int AccountId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public string? Permissions { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
}
