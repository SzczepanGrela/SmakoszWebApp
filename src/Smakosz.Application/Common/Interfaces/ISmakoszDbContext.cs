using Microsoft.EntityFrameworkCore;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.Generator;
using Smakosz.Domain.Entities.System;

namespace Smakosz.Application.Common.Interfaces;

public interface ISmakoszDbContext : IAsyncDisposable
{
    DbSet<City> Cities { get; }
    DbSet<CuisineType> CuisineTypes { get; }
    DbSet<RestaurantTheme> RestaurantThemes { get; }
    DbSet<Ingredient> Ingredients { get; }
    DbSet<DishArchetype> DishArchetypes { get; }
    DbSet<DishVariant> DishVariants { get; }
    DbSet<Tag> Tags { get; }

    DbSet<User> Users { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<UserFollow> UserFollows { get; }
    DbSet<VerificationCode> VerificationCodes { get; }
    DbSet<SearchHistory> SearchHistories { get; }
    DbSet<SearchAutocomplete> SearchAutocompletes { get; }

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

    DbSet<ReportReasonDefinition> ReportReasonDefinitions { get; }
    DbSet<RejectionReason> RejectionReasons { get; }

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
    DbSet<PushSubscription> PushSubscriptions { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<SystemConfig> SystemConfigs { get; }
    DbSet<SystemNode> SystemNodes { get; }
    DbSet<ServiceAccount> ServiceAccounts { get; }
    DbSet<SystemJob> SystemJobs { get; }
    DbSet<JobProgress> JobProgresses { get; }
    DbSet<SystemLog> SystemLogs { get; }
    DbSet<SecurityLog> SecurityLogs { get; }
    DbSet<EmailLog> EmailLogs { get; }
    DbSet<ModerationLog> ModerationLogs { get; }
    DbSet<UserActionLog> UserActionLogs { get; }
    DbSet<AiLog> AiLogs { get; }
    DbSet<SystemTicket> SystemTickets { get; }
    DbSet<BannedIdentifier> BannedIdentifiers { get; }
    DbSet<ForbiddenWord> ForbiddenWords { get; }
    DbSet<FileToDelete> FilesToDelete { get; }
    DbSet<SiteStats> SiteStats { get; }
    DbSet<HomePageCache> HomePageCaches { get; }
    DbSet<ModerationResult> ModerationResults { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
