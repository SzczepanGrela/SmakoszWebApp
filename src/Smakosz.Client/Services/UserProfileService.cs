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
            Bio = (string?)null
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
}
