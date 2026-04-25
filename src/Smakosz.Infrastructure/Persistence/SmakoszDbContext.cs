using Microsoft.EntityFrameworkCore;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.Generator;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Interfaces;

namespace Smakosz.Infrastructure.Persistence;

public class SmakoszDbContext : DbContext, ISmakoszDbContext
{
    public SmakoszDbContext(DbContextOptions<SmakoszDbContext> options) : base(options)
    {
    }

    public override int SaveChanges()
    {
        ApplyConventions();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyConventions();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyConventions()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is IAuditableEntity auditable)
            {
                if (entry.State == EntityState.Added)
                    auditable.CreatedAt ??= now;
                if (entry.State == EntityState.Modified)
                    auditable.UpdatedAt = now;
            }

            if (entry.Entity is ISoftDeletable soft && entry.State == EntityState.Deleted)
            {
                entry.State = EntityState.Modified;
                soft.IsDeleted = true;
                soft.DeletedAt = now;
            }

            if (entry.Entity is IVersioned versioned && entry.State == EntityState.Modified)
                versioned.Version++;

            if (entry.Entity is IHasPublicId pub
                && entry.State == EntityState.Added
                && pub.PublicId == Guid.Empty)
                pub.PublicId = Guid.NewGuid();

            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is User user && string.IsNullOrEmpty(user.Slug))
                    user.Slug = SlugGenerator.GenerateSlug(user.Username);
                if (entry.Entity is Restaurant rest && string.IsNullOrEmpty(rest.Slug))
                    rest.Slug = SlugGenerator.GenerateSlug(rest.RestaurantName);
                if (entry.Entity is Dish dish && string.IsNullOrEmpty(dish.Slug))
                    dish.Slug = SlugGenerator.GenerateSlug(dish.DishName);
            }
        }
    }

    public DbSet<City> Cities => Set<City>();
    public DbSet<CuisineType> CuisineTypes => Set<CuisineType>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<DishArchetype> DishArchetypes => Set<DishArchetype>();
    public DbSet<DishVariant> DishVariants => Set<DishVariant>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ReportReasonDefinition> ReportReasonDefinitions => Set<ReportReasonDefinition>();
    public DbSet<RejectionReason> RejectionReasons => Set<RejectionReason>();

    public DbSet<User> Users => Set<User>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();
    public DbSet<UserNotificationSettings> UserNotificationSettings => Set<UserNotificationSettings>();
    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<SearchHistory> SearchHistories => Set<SearchHistory>();
    public DbSet<UserFollow> UserFollows => Set<UserFollow>();

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<RestaurantOpeningHours> RestaurantOpeningHours => Set<RestaurantOpeningHours>();
    public DbSet<MenuSection> MenuSections => Set<MenuSection>();
    public DbSet<Dish> Dishes => Set<Dish>();
    public DbSet<DishSectionAssignment> DishSectionAssignments => Set<DishSectionAssignment>();
    public DbSet<DishIngredient> DishIngredients => Set<DishIngredient>();
    public DbSet<DishTag> DishTags => Set<DishTag>();
    public DbSet<RestaurantTag> RestaurantTags => Set<RestaurantTag>();
    public DbSet<SavedDish> SavedDishes => Set<SavedDish>();
    public DbSet<FavoriteRestaurant> FavoriteRestaurants => Set<FavoriteRestaurant>();

    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewLike> ReviewLikes => Set<ReviewLike>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<ReportReasonAssignment> ReportReasonAssignments => Set<ReportReasonAssignment>();
    public DbSet<DataCorrectionRequest> DataCorrectionRequests => Set<DataCorrectionRequest>();
    public DbSet<RestaurantEditRequest> RestaurantEditRequests => Set<RestaurantEditRequest>();
    public DbSet<IngredientSuggestion> IngredientSuggestions => Set<IngredientSuggestion>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();
    public DbSet<SystemNode> SystemNodes => Set<SystemNode>();
    public DbSet<ServiceAccount> ServiceAccounts => Set<ServiceAccount>();
    public DbSet<SystemJob> SystemJobs => Set<SystemJob>();
    public DbSet<JobProgress> JobProgresses => Set<JobProgress>();
    public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
    public DbSet<SecurityLog> SecurityLogs => Set<SecurityLog>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
    public DbSet<ModerationLog> ModerationLogs => Set<ModerationLog>();
    public DbSet<AiLog> AiLogs => Set<AiLog>();
    public DbSet<SystemTicket> SystemTickets => Set<SystemTicket>();
    public DbSet<BannedIdentifier> BannedIdentifiers => Set<BannedIdentifier>();
    public DbSet<ForbiddenWord> ForbiddenWords => Set<ForbiddenWord>();
public DbSet<FileToDelete> FilesToDelete => Set<FileToDelete>();
    public DbSet<SiteStats> SiteStats => Set<SiteStats>();
    public DbSet<HomePageCache> HomePageCaches => Set<HomePageCache>();
    public DbSet<ModerationResult> ModerationResults => Set<ModerationResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("unaccent");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmakoszDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            return;

        optionsBuilder.UseSnakeCaseNamingConvention();
    }
}
