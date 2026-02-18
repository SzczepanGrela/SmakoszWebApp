using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities;

public class RejectionReason
{
    public string ReasonCode { get; set; } = string.Empty;
    public RejectionReasonCategory Category { get; set; }
    public string AdminLabel { get; set; } = string.Empty;
    public string UserMessageTemplate { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
}
