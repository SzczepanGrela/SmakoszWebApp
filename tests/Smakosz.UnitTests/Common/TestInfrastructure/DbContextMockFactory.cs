using MockQueryable.NSubstitute;
using NSubstitute;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;

namespace Smakosz.UnitTests.Common.TestInfrastructure;

public class MockDbSets
{
    public List<User> Users { get; } = new();
    public List<Restaurant> Restaurants { get; } = new();
    public List<Dish> Dishes { get; } = new();
    public List<Review> Reviews { get; } = new();
    public List<UserSession> UserSessions { get; } = new();
    public List<FavoriteRestaurant> FavoriteRestaurants { get; } = new();
    public List<SavedDish> SavedDishes { get; } = new();
    public List<ReviewLike> ReviewLikes { get; } = new();
    public List<VerificationCode> VerificationCodes { get; } = new();
    public List<City> Cities { get; } = new();
    public List<CuisineType> CuisineTypes { get; } = new();
    public List<MenuSection> MenuSections { get; } = new();
    public List<RestaurantOpeningHours> OpeningHours { get; } = new();
    public List<UserFollow> UserFollows { get; } = new();
    public List<Notification> Notifications { get; } = new();
    public List<UserNotificationSettings> NotificationSettings { get; } = new();
    public List<MediaAsset> MediaAssets { get; } = new();
    public List<Report> Reports { get; } = new();
    public List<Ingredient> Ingredients { get; } = new();
    public List<DishIngredient> DishIngredients { get; } = new();
    public List<DataCorrectionRequest> DataCorrectionRequests { get; } = new();
    public List<RestaurantEditRequest> RestaurantEditRequests { get; } = new();
    public List<IngredientSuggestion> IngredientSuggestions { get; } = new();
    public List<DishSectionAssignment> DishSectionAssignments { get; } = new();
    public List<DishTag> DishTags { get; } = new();
    public List<RestaurantTag> RestaurantTags { get; } = new();
    public List<DishArchetype> DishArchetypes { get; } = new();
    public List<DishVariant> DishVariants { get; } = new();
    public List<Tag> Tags { get; } = new();
    public List<SearchHistory> SearchHistories { get; } = new();
    public List<ReportReasonDefinition> ReportReasonDefinitions { get; } = new();
    public List<RejectionReason> RejectionReasons { get; } = new();
    public List<ReportReasonAssignment> ReportReasonAssignments { get; } = new();
    public List<AuditLog> AuditLogs { get; } = new();
    public List<SystemConfig> SystemConfigs { get; } = new();
    public List<SystemNode> SystemNodes { get; } = new();
    public List<ServiceAccount> ServiceAccounts { get; } = new();
    public List<SystemJob> SystemJobs { get; } = new();
    public List<JobProgress> JobProgresses { get; } = new();
    public List<SystemLog> SystemLogs { get; } = new();
    public List<SecurityLog> SecurityLogs { get; } = new();
    public List<EmailLog> EmailLogs { get; } = new();
    public List<ModerationLog> ModerationLogs { get; } = new();
    public List<AiLog> AiLogs { get; } = new();
    public List<SystemTicket> SystemTickets { get; } = new();
    public List<BannedIdentifier> BannedIdentifiers { get; } = new();
    public List<ForbiddenWord> ForbiddenWords { get; } = new();
    public List<RefreshToken> RefreshTokens { get; } = new();
    public List<FileToDelete> FilesToDelete { get; } = new();
    public List<SiteStats> SiteStats { get; } = new();
    public List<ModerationResult> ModerationResults { get; } = new();
}

public static class DbContextMockFactory
{
    public static (ISmakoszDbContext Context, MockDbSets Sets) Create()
    {
        var context = Substitute.For<ISmakoszDbContext>();
        var sets = new MockDbSets();

        WireAll(context, sets);

        context.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);

        return (context, sets);
    }

    private static void WireDbSet<T>(
        Func<List<T>> listAccessor,
        Action<Microsoft.EntityFrameworkCore.DbSet<T>> setter,
        List<T> backingList) where T : class
    {
        var mock = backingList.AsQueryable().BuildMockDbSet();

        mock.When(x => x.Add(Arg.Any<T>()))
            .Do(ci => backingList.Add(ci.Arg<T>()));

        mock.When(x => x.Remove(Arg.Any<T>()))
            .Do(ci => backingList.Remove(ci.Arg<T>()));

        setter(mock);
    }

    private static void WireAll(ISmakoszDbContext context, MockDbSets sets)
    {
        WireDbSet(() => sets.Users, v => context.Users.Returns(v), sets.Users);
        WireDbSet(() => sets.Restaurants, v => context.Restaurants.Returns(v), sets.Restaurants);
        WireDbSet(() => sets.Dishes, v => context.Dishes.Returns(v), sets.Dishes);
        WireDbSet(() => sets.Reviews, v => context.Reviews.Returns(v), sets.Reviews);
        WireDbSet(() => sets.UserSessions, v => context.UserSessions.Returns(v), sets.UserSessions);
        WireDbSet(() => sets.FavoriteRestaurants, v => context.FavoriteRestaurants.Returns(v), sets.FavoriteRestaurants);
        WireDbSet(() => sets.SavedDishes, v => context.SavedDishes.Returns(v), sets.SavedDishes);
        WireDbSet(() => sets.ReviewLikes, v => context.ReviewLikes.Returns(v), sets.ReviewLikes);
        WireDbSet(() => sets.VerificationCodes, v => context.VerificationCodes.Returns(v), sets.VerificationCodes);
        WireDbSet(() => sets.Cities, v => context.Cities.Returns(v), sets.Cities);
        WireDbSet(() => sets.CuisineTypes, v => context.CuisineTypes.Returns(v), sets.CuisineTypes);
        WireDbSet(() => sets.MenuSections, v => context.MenuSections.Returns(v), sets.MenuSections);
        WireDbSet(() => sets.OpeningHours, v => context.RestaurantOpeningHours.Returns(v), sets.OpeningHours);
        WireDbSet(() => sets.UserFollows, v => context.UserFollows.Returns(v), sets.UserFollows);
        WireDbSet(() => sets.Notifications, v => context.Notifications.Returns(v), sets.Notifications);
        WireDbSet(() => sets.NotificationSettings, v => context.UserNotificationSettings.Returns(v), sets.NotificationSettings);
        WireDbSet(() => sets.MediaAssets, v => context.MediaAssets.Returns(v), sets.MediaAssets);
        WireDbSet(() => sets.Reports, v => context.Reports.Returns(v), sets.Reports);
        WireDbSet(() => sets.Ingredients, v => context.Ingredients.Returns(v), sets.Ingredients);
        WireDbSet(() => sets.DishIngredients, v => context.DishIngredients.Returns(v), sets.DishIngredients);
        WireDbSet(() => sets.DataCorrectionRequests, v => context.DataCorrectionRequests.Returns(v), sets.DataCorrectionRequests);
        WireDbSet(() => sets.RestaurantEditRequests, v => context.RestaurantEditRequests.Returns(v), sets.RestaurantEditRequests);
        WireDbSet(() => sets.IngredientSuggestions, v => context.IngredientSuggestions.Returns(v), sets.IngredientSuggestions);
        WireDbSet(() => sets.DishSectionAssignments, v => context.DishSectionAssignments.Returns(v), sets.DishSectionAssignments);
        WireDbSet(() => sets.DishTags, v => context.DishTags.Returns(v), sets.DishTags);
        WireDbSet(() => sets.RestaurantTags, v => context.RestaurantTags.Returns(v), sets.RestaurantTags);
        WireDbSet(() => sets.DishArchetypes, v => context.DishArchetypes.Returns(v), sets.DishArchetypes);
        WireDbSet(() => sets.DishVariants, v => context.DishVariants.Returns(v), sets.DishVariants);
        WireDbSet(() => sets.Tags, v => context.Tags.Returns(v), sets.Tags);
        WireDbSet(() => sets.SearchHistories, v => context.SearchHistories.Returns(v), sets.SearchHistories);
        WireDbSet(() => sets.ReportReasonDefinitions, v => context.ReportReasonDefinitions.Returns(v), sets.ReportReasonDefinitions);
        WireDbSet(() => sets.RejectionReasons, v => context.RejectionReasons.Returns(v), sets.RejectionReasons);
        WireDbSet(() => sets.ReportReasonAssignments, v => context.ReportReasonAssignments.Returns(v), sets.ReportReasonAssignments);
        WireDbSet(() => sets.AuditLogs, v => context.AuditLogs.Returns(v), sets.AuditLogs);
        WireDbSet(() => sets.SystemConfigs, v => context.SystemConfigs.Returns(v), sets.SystemConfigs);
        WireDbSet(() => sets.SystemNodes, v => context.SystemNodes.Returns(v), sets.SystemNodes);
        WireDbSet(() => sets.ServiceAccounts, v => context.ServiceAccounts.Returns(v), sets.ServiceAccounts);
        WireDbSet(() => sets.SystemJobs, v => context.SystemJobs.Returns(v), sets.SystemJobs);
        WireDbSet(() => sets.JobProgresses, v => context.JobProgresses.Returns(v), sets.JobProgresses);
        WireDbSet(() => sets.SystemLogs, v => context.SystemLogs.Returns(v), sets.SystemLogs);
        WireDbSet(() => sets.SecurityLogs, v => context.SecurityLogs.Returns(v), sets.SecurityLogs);
        WireDbSet(() => sets.EmailLogs, v => context.EmailLogs.Returns(v), sets.EmailLogs);
        WireDbSet(() => sets.ModerationLogs, v => context.ModerationLogs.Returns(v), sets.ModerationLogs);
        WireDbSet(() => sets.AiLogs, v => context.AiLogs.Returns(v), sets.AiLogs);
        WireDbSet(() => sets.SystemTickets, v => context.SystemTickets.Returns(v), sets.SystemTickets);
        WireDbSet(() => sets.BannedIdentifiers, v => context.BannedIdentifiers.Returns(v), sets.BannedIdentifiers);
        WireDbSet(() => sets.ForbiddenWords, v => context.ForbiddenWords.Returns(v), sets.ForbiddenWords);
        WireDbSet(() => sets.RefreshTokens, v => context.RefreshTokens.Returns(v), sets.RefreshTokens);
        WireDbSet(() => sets.FilesToDelete, v => context.FilesToDelete.Returns(v), sets.FilesToDelete);
        WireDbSet(() => sets.SiteStats, v => context.SiteStats.Returns(v), sets.SiteStats);
        WireDbSet(() => sets.ModerationResults, v => context.ModerationResults.Returns(v), sets.ModerationResults);
    }

    public static void Refresh(ISmakoszDbContext context, MockDbSets sets)
    {
        WireAll(context, sets);
    }
}
