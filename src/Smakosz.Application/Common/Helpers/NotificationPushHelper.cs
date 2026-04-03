using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.Application.Common.Helpers;

public static class NotificationPushHelper
{
    public static (bool SendPush, PushStatus PushStatus) Resolve(
        UserNotificationSettings? settings,
        NotificationType type)
    {
        var enabled = type switch
        {
            NotificationType.Like => settings?.PushLike ?? true,
            NotificationType.Follow => settings?.PushFollow ?? true,
            NotificationType.System => settings?.PushSystem ?? true,
            NotificationType.Security => settings?.PushSystem ?? true,
            _ => false
        };

        return enabled
            ? (true, PushStatus.Pending)
            : (false, PushStatus.None);
    }
}
