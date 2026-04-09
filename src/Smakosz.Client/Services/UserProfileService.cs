using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class UserProfileService : IUserProfileService
{
    private readonly SmakoszApiClient _api;

    public UserProfileService(SmakoszApiClient api) => _api = api;

    public Task<MyProfileDto?> GetMyProfileAsync()
        => _api.GetAsync<MyProfileDto>("/api/me");

    public Task<UserPublicProfileDto?> GetUserProfileAsync(string slug)
        => _api.GetAsync<UserPublicProfileDto>($"/api/users/{slug}");

    public Task<PagedResult<UserSummaryDto>?> GetFollowersAsync(string slug, int page = 1)
        => _api.GetAsync<PagedResult<UserSummaryDto>>($"/api/users/{slug}/followers?page={page}");

    public Task<PagedResult<UserSummaryDto>?> GetFollowingAsync(string slug, int page = 1)
        => _api.GetAsync<PagedResult<UserSummaryDto>>($"/api/users/{slug}/following?page={page}");

    public Task<PagedResult<DishCardDto>?> GetSavedDishesAsync(int page = 1)
        => _api.GetAsync<PagedResult<DishCardDto>>($"/api/me/saved-dishes?page={page}");

    public Task<PagedResult<RestaurantCardDto>?> GetSavedRestaurantsAsync(int page = 1)
        => _api.GetAsync<PagedResult<RestaurantCardDto>>($"/api/me/favorite-restaurants?page={page}");

    public async Task<List<SessionDto>> GetSessionsAsync()
        => await _api.GetAsync<List<SessionDto>>("/api/me/sessions") ?? [];

    public async Task<bool> UpdateProfileAsync(MyProfileDto profile)
    {
        var response = await _api.PutApiResponseAsync<object>("/api/me", new
        {
            profile.Username,
            Bio = (string?)null,
            profile.AvatarUrl
        });
        return response.Success;
    }

    public async Task<bool> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var response = await _api.PutApiResponseAsync<object>("/api/me/password", new
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        });
        return response.Success;
    }

    public Task<NotificationSettingsDto?> GetNotificationSettingsAsync()
        => _api.GetAsync<NotificationSettingsDto>("/api/me/notification-settings");

    public async Task<bool> UpdateNotificationSettingsAsync(NotificationSettingsDto settings)
    {
        var response = await _api.PutApiResponseAsync<object>("/api/me/notification-settings", settings);
        return response.Success;
    }

    public async Task<bool> SaveDishAsync(string dishSlug)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/me/saved-dishes/{dishSlug}", null);
        return response.Success;
    }

    public Task<bool> UnsaveDishAsync(string dishSlug)
        => _api.DeleteAsync($"/api/me/saved-dishes/{dishSlug}");

    public async Task<bool> FavoriteRestaurantAsync(string restaurantSlug)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/me/favorite-restaurants/{restaurantSlug}", null);
        return response.Success;
    }

    public Task<bool> UnfavoriteRestaurantAsync(string restaurantSlug)
        => _api.DeleteAsync($"/api/me/favorite-restaurants/{restaurantSlug}");

    public async Task<bool> FollowUserAsync(string slug)
    {
        var response = await _api.PostApiResponseAsync<object>($"/api/me/following/{slug}", null);
        return response.Success;
    }

    public Task<bool> UnfollowUserAsync(string slug)
        => _api.DeleteAsync($"/api/me/following/{slug}");

    public Task<bool> RevokeSessionAsync(long sessionId)
        => _api.DeleteAsync($"/api/me/sessions/{sessionId}");

    public async Task<bool> RevokeAllSessionsAsync()
    {
        var response = await _api.DeleteApiResponseAsync("/api/me/sessions");
        return response.Success;
    }

    public Task<PagedResult<ReviewCardDto>?> GetMyReviewsAsync(int page = 1)
        => _api.GetAsync<PagedResult<ReviewCardDto>>($"/api/me/reviews?page={page}");

    public async Task<bool> RequestAccountDeletionAsync(string password)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/me/delete-account/request", new { Password = password });
        return response.Success;
    }

    public async Task<bool> ConfirmAccountDeletionAsync(string code)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/me/delete-account/confirm", new { Code = code });
        return response.Success;
    }

    public async Task<bool> Enable2faAsync()
    {
        var response = await _api.PostApiResponseAsync<object>("/api/me/2fa/enable", null);
        return response.Success;
    }

    public async Task<bool> Confirm2faAsync(string code)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/me/2fa/confirm", new { Code = code });
        return response.Success;
    }

    public async Task<bool> Disable2faAsync(string password)
    {
        var response = await _api.PostApiResponseAsync<object>("/api/me/2fa/disable", new { Password = password });
        return response.Success;
    }
}
