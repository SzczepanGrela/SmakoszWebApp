using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class MenuSection : IModerable
{
    public int SectionId { get; set; }
    public int RestaurantId { get; set; }
    public string SectionName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public ContentModerationStatus ModerationStatus { get; set; } = ContentModerationStatus.None;

    public Restaurant Restaurant { get; set; } = null!;
}
