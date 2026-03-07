using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class Review : IAuditableEntity, ISoftDeletable, IHasPublicId, IVersioned, IModerable
{
    public int ReviewId { get; set; }
    public Guid PublicId { get; set; }
    public int UserId { get; set; }
    public int RestaurantId { get; set; }
    public int DishId { get; set; }
    public DateOnly VisitDate { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int DishRating { get; set; }
    public int ServiceRating { get; set; }
    public int CleanlinessRating { get; set; }
    public int AmbianceRating { get; set; }
    public string? Content { get; set; }
    public bool IsVisible { get; set; }
    public ContentModerationStatus ModerationStatus { get; set; } = ContentModerationStatus.None;
    public string? ContentRejectionReason { get; set; }
    public int HelpfulCount { get; set; }
    public bool? IsApproved { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int Version { get; set; } = 1;

    public User User { get; set; } = null!;
    public Restaurant Restaurant { get; set; } = null!;
    public Dish Dish { get; set; } = null!;
}
