using Microsoft.EntityFrameworkCore;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Application.Common.Interfaces;

public interface ISmakoszDbContext
{
    // Dictionary
    DbSet<City> Cities { get; }
    DbSet<CuisineType> CuisineTypes { get; }
    DbSet<Ingredient> Ingredients { get; }
    DbSet<DishArchetype> DishArchetypes { get; }
    DbSet<DishVariant> DishVariants { get; }
    DbSet<Tag> Tags { get; }

    // Identity
    DbSet<User> Users { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<UserFollow> UserFollows { get; }
    DbSet<VerificationCode> VerificationCodes { get; }
    DbSet<SearchHistory> SearchHistories { get; }

    // Restaurant / Menu
    DbSet<Restaurant> Restaurants { get; }
    DbSet<RestaurantOpeningHours> RestaurantOpeningHours { get; }
    DbSet<MenuSection> MenuSections { get; }
    DbSet<Dish> Dishes { get; }
    DbSet<DishSectionAssignment> DishSectionAssignments { get; }
    DbSet<DishIngredient> DishIngredients { get; }
    DbSet<DishTag> DishTags { get; }
    DbSet<RestaurantTag> RestaurantTags { get; }
    DbSet<SavedDish> SavedDishes { get; }
    DbSet<FavoriteRestaurant> FavoriteRestaurants { get; }

    // Dictionary (additional)
    DbSet<ReportReasonDefinition> ReportReasonDefinitions { get; }
    DbSet<RejectionReason> RejectionReasons { get; }

    // Social / Content
    DbSet<MediaAsset> MediaAssets { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<Review> Reviews { get; }
    DbSet<ReviewLike> ReviewLikes { get; }
    DbSet<Report> Reports { get; }
    DbSet<ReportReasonAssignment> ReportReasonAssignments { get; }
    DbSet<DataCorrectionRequest> DataCorrectionRequests { get; }
    DbSet<RestaurantEditRequest> RestaurantEditRequests { get; }
    DbSet<IngredientSuggestion> IngredientSuggestions { get; }
    DbSet<UserNotificationSettings> UserNotificationSettings { get; }

    // Audit
    DbSet<AuditLog> AuditLogs { get; }

    // System
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SystemConfig> SystemConfigs { get; }
    DbSet<SystemNode> SystemNodes { get; }
    DbSet<ServiceAccount> ServiceAccounts { get; }
    DbSet<SystemJob> SystemJobs { get; }
    DbSet<JobProgress> JobProgresses { get; }
    DbSet<SystemLog> SystemLogs { get; }
    DbSet<SecurityLog> SecurityLogs { get; }
    DbSet<EmailLog> EmailLogs { get; }
    DbSet<ModerationLog> ModerationLogs { get; }
    DbSet<AiLog> AiLogs { get; }
    DbSet<SystemTicket> SystemTickets { get; }
    DbSet<BannedIdentifier> BannedIdentifiers { get; }
    DbSet<ForbiddenWord> ForbiddenWords { get; }
    DbSet<FileToDelete> FilesToDelete { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
