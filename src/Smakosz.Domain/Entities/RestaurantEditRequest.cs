using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class RestaurantEditRequest : IVersioned
{
    public int RequestId { get; set; }
    public int RestaurantId { get; set; }
    public int UserId { get; set; }
    public EditRequestStatus Status { get; set; } = EditRequestStatus.Pending;
    public EditRequestChangeType ChangeType { get; set; } = EditRequestChangeType.General;
    public EditRequestChangeScope ChangeScope { get; set; } = EditRequestChangeScope.Restaurant;
    public int? TargetEntityId { get; set; }
    public string Payload { get; set; } = "{}";
    public string? NewName { get; set; }
    public string? NewDescription { get; set; }
    public string? NewAddress { get; set; }
    public string? NewCuisineType { get; set; }
    public string? NewPhone { get; set; }
    public string? NewWebsite { get; set; }
    public string? NewImageUrl { get; set; }
    public string? NewImageBlurhash { get; set; }
    public ContentModerationStatus ModerationStatus { get; set; } = ContentModerationStatus.None;
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? AdminNote { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByAdminId { get; set; }
    public int Version { get; set; } = 1;

    public Restaurant Restaurant { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? Reviewer { get; set; }
    public User? ResolvedByAdmin { get; set; }
}
