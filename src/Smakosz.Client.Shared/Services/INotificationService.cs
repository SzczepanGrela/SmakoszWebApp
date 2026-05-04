using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface INotificationService
{
    event Action? UnreadCountChanged;
    Task<PagedResult<NotificationDto>?> GetNotificationsAsync(int page = 1);
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(int id);
    Task MarkAllAsReadAsync();
}
