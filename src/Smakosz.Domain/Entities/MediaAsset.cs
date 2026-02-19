using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class MediaAsset : IHasPublicId, IVersioned
{
    public long AssetId { get; set; }
    public Guid PublicId { get; set; }
    public MediaEntityType EntityType { get; set; }
    public int EntityId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? Blurhash { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public bool IsPrimary { get; set; }
    public MediaAssetStatus Status { get; set; } = MediaAssetStatus.Approved;
    public int? UploadedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? RejectionReason { get; set; }
    public decimal? AiNsfwScore { get; set; }
    public decimal? AiOnTopicScore { get; set; }
    public string? AiVerdict { get; set; }
    public string? AiModelVersion { get; set; }
    public DateTime? AiProcessedAt { get; set; }
    public string? CreditText { get; set; }
    public int Version { get; set; } = 1;

    public User? Uploader { get; set; }
}
