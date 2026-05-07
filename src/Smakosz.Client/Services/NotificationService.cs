using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class NotificationService : INotificationService
{
    private readonly SmakoszApiClient _api;

    public NotificationService(SmakoszApiClient api) => _api = api;

    public event Action? UnreadCountChanged;

    public Task<PagedResult<NotificationDto>?> GetNotificationsAsync(int page = 1)
        => _api.GetAsync<PagedResult<NotificationDto>>($"/api/me/notifications?page={page}");

    public async Task<int> GetUnreadCountAsync()
        => await _api.GetAsync<int>("/api/me/notifications/unread-count");

    public async Task<bool> MarkAsReadAsync(Guid publicId)
    {
        var response = await _api.PutApiResponseAsync<object>($"/api/me/notifications/{publicId}/read", null);
        if (response.Success) UnreadCountChanged?.Invoke();
        return response.Success;
    }

    public async Task<bool> MarkAllAsReadAsync()
    {
        var response = await _api.PutApiResponseAsync<object>("/api/me/notifications/read-all", null);
        if (response.Success) UnreadCountChanged?.Invoke();
        return response.Success;
    }
}
