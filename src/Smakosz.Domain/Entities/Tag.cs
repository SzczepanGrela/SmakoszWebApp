using Smakosz.Domain.Enums;

namespace Smakosz.Domain.Entities;

public class Tag
{
    public int TagId { get; set; }
    public string TagName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public TagTargetEntity TargetEntity { get; set; } = TagTargetEntity.Both;
    public string? DisplayColor { get; set; }
    public DateTime CreatedAt { get; set; }
}
