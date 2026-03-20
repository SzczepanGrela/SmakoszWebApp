using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities;

public class AuditLog
{
    public long AuditLogId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public int RecordId { get; set; }
    public AuditOperation Operation { get; set; }
    public string ChangedBy { get; set; } = "system";
    public DateTime ChangedAt { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
}
