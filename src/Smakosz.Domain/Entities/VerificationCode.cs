using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities;

public class VerificationCode
{
    public int VerificationCodeId { get; set; }
    public int UserId { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public VerificationCodeType Type { get; set; }
    public string? Payload { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int AttemptsCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
