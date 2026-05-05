using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class Restaurant : IAuditableEntity, IHasPublicId, IVersioned, IModerable, IHasPhone
{
    public int RestaurantId { get; set; }
    public Guid PublicId { get; set; }
    public int CityId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    public int CuisineTypeId { get; set; }
    public int? PriceLevel { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Description { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageBlurhash { get; set; }
    public RestaurantStatus Status { get; set; } = RestaurantStatus.Active;
    public bool IsVerified { get; set; }
    public int? OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public double? AvgService { get; set; }
    public double? AvgCleanliness { get; set; }
    public double? AvgAmbiance { get; set; }
    public double? AvgFoodScore { get; set; }
    public decimal? TrendingScore { get; set; }
    public int Version { get; set; } = 1;
    public DateTime? VerifiedAt { get; set; }
    public int? VerifiedBy { get; set; }
    public ContentModerationStatus ModerationStatus { get; set; } = ContentModerationStatus.None;

    #region Generator-Only Fields
    public double? SecretPriceMultiplier { get; set; }
    public double? SecretOverallFoodQuality { get; set; }
    public double? SecretServiceQuality { get; set; }
    public double? SecretCleanlinessScore { get; set; }
    public string? SecretAmbianceType { get; set; }
    public double? SecretAmbianceQuality { get; set; }
    public string? SecretArchetypeModifiers { get; set; }
    public string? SecretMenuBlueprint { get; set; }
    #endregion

    public City City { get; set; } = null!;
    public CuisineType Cuisine { get; set; } = null!;
    public User? Owner { get; set; }

    public ICollection<RestaurantOpeningHours> OpeningHours { get; set; } = new List<RestaurantOpeningHours>();
    public ICollection<MenuSection> MenuSections { get; set; } = new List<MenuSection>();
}
