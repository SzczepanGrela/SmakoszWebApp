using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public class NotificationService : INotificationService
{
    private readonly SmakoszApiClient _api;

    public NotificationService(SmakoszApiClient api) => _api = api;

    public Task<PagedResult<NotificationDto>?> GetNotificationsAsync(int page = 1)
        => _api.GetAsync<PagedResult<NotificationDto>>($"/api/me/notifications?page={page}");

    public async Task<int> GetUnreadCountAsync()
        => await _api.GetAsync<int>("/api/me/notifications/unread-count");

    public async Task MarkAsReadAsync(int id)
        => await _api.PutApiResponseAsync<object>($"/api/me/notifications/{id}/read", null);

    public async Task MarkAllAsReadAsync()
        => await _api.PutApiResponseAsync<object>("/api/me/notifications/read-all", null);
}
