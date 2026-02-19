using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class Report : IVersioned
{
    public int ReportId { get; set; }
    public int ReporterId { get; set; }
    public ReportEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public string? Description { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByAdminId { get; set; }
    public int Version { get; set; } = 1;

    public User Reporter { get; set; } = null!;
    public User? ResolvedByAdmin { get; set; }
}
