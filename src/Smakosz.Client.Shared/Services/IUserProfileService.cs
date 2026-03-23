using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface IUserProfileService
{
    Task<MyProfileDto?> GetMyProfileAsync();
    Task<UserPublicProfileDto?> GetUserProfileAsync(string slug);
    Task<PagedResult<UserSummaryDto>?> GetFollowersAsync(string slug, int page = 1);
    Task<PagedResult<UserSummaryDto>?> GetFollowingAsync(string slug, int page = 1);
    Task<PagedResult<DishCardDto>?> GetSavedDishesAsync(int page = 1);
    Task<PagedResult<RestaurantCardDto>?> GetSavedRestaurantsAsync(int page = 1);
    Task<List<SessionDto>> GetSessionsAsync();
    Task<bool> UpdateProfileAsync(MyProfileDto profile);
    Task<bool> ChangePasswordAsync(string currentPassword, string newPassword);
    Task<NotificationSettingsDto?> GetNotificationSettingsAsync();
    Task<bool> UpdateNotificationSettingsAsync(NotificationSettingsDto settings);
    Task<bool> SaveDishAsync(string dishSlug);
    Task<bool> UnsaveDishAsync(string dishSlug);
    Task<bool> FavoriteRestaurantAsync(string restaurantSlug);
    Task<bool> UnfavoriteRestaurantAsync(string restaurantSlug);
    Task<bool> FollowUserAsync(string slug);
    Task<bool> UnfollowUserAsync(string slug);
    Task<bool> RevokeSessionAsync(long sessionId);
    Task<bool> RevokeAllSessionsAsync();
    Task<PagedResult<ReviewCardDto>?> GetMyReviewsAsync(int page = 1);
    Task<bool> RequestAccountDeletionAsync(string password);
    Task<bool> ConfirmAccountDeletionAsync(string code);
}
