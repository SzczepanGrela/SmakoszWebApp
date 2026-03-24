using Smakosz.Client.Models;

namespace Smakosz.Client.Services;

public interface INotificationService
{
    Task<PagedResult<NotificationDto>?> GetNotificationsAsync(int page = 1);
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(Guid id);
    Task MarkAllAsReadAsync();
}
