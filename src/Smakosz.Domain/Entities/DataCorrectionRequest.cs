using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class DataCorrectionRequest : IVersioned
{
    public int RequestId { get; set; }
    public int? UserId { get; set; }
    public int RestaurantId { get; set; }
    public DataCorrectionIssueType IssueType { get; set; }
    public string? Description { get; set; }
    public string? ProposedValue { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? CreatedAt { get; set; }
    public int Version { get; set; } = 1;

    public User? User { get; set; }
    public Restaurant Restaurant { get; set; } = null!;
}
