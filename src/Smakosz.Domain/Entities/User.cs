using Smakosz.Domain.Enums;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Domain.Entities;

public class User : IAuditableEntity, ISoftDeletable, IHasPublicId
{
    public int UserId { get; set; }
    public Guid PublicId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int? RestaurantId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool EmailVerified { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? SecurityStamp { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? AvatarBlurhash { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsBanned { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public int FollowersCount { get; set; }
    public int FollowingCount { get; set; }
    public string? Slug { get; set; }
    public bool Is2faEnabled { get; set; }
    public int ReviewCount { get; set; }
    public int PhotoCount { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }

    #region Generator-Only Fields
    public int? SecretHomeCityId { get; set; }
    public int? SecretTotalReviewCount { get; set; }
    public double? SecretTravelPropensity { get; set; }
    public string? SecretEnjoyedArchetypes { get; set; }
    public double? SecretChanceDineRandom { get; set; }
    public double? SecretChancePickRandomDish { get; set; }
    public double? SecretCrossImpactFactor { get; set; }
    public double? SecretMoodPropensity { get; set; }
    public bool SecretIsInfluencer { get; set; }
    public double SecretRatingBaseline { get; set; } = 6.0;
    public string SecretCharacteristicsVector { get; set; } = "{}";
    public string? SecretIngredientPreferences { get; set; }
    public string? SecretCleanlinessPreference { get; set; }
    public string? SecretPreferredAmbiance { get; set; }
    public City? SecretHomeCity { get; set; }
    #endregion

    public UserNotificationSettings? NotificationSettings { get; set; }
    public ICollection<VerificationCode> VerificationCodes { get; set; } = new List<VerificationCode>();
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public ICollection<SearchHistory> SearchHistories { get; set; } = new List<SearchHistory>();
}
