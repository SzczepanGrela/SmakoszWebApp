using FluentAssertions;
using Smakosz.Application.Common.Helpers;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.UnitTests.Common.Helpers;

[Trait("Category", "Helpers")]
public class NotificationPushHelperTests
{
    [Fact]
    public void Resolve_LikeWithPushLikeEnabled_ReturnsPending()
    {
        var settings = new UserNotificationSettings { PushLike = true };

        var (sendPush, status) = NotificationPushHelper.Resolve(settings, NotificationType.Like);

        sendPush.Should().BeTrue();
        status.Should().Be(PushStatus.Pending);
    }

    [Fact]
    public void Resolve_LikeWithPushLikeDisabled_ReturnsNone()
    {
        var settings = new UserNotificationSettings { PushLike = false };

        var (sendPush, status) = NotificationPushHelper.Resolve(settings, NotificationType.Like);

        sendPush.Should().BeFalse();
        status.Should().Be(PushStatus.None);
    }

    [Fact]
    public void Resolve_FollowWithPushFollowEnabled_ReturnsPending()
    {
        var settings = new UserNotificationSettings { PushFollow = true };

        var (sendPush, status) = NotificationPushHelper.Resolve(settings, NotificationType.Follow);

        sendPush.Should().BeTrue();
        status.Should().Be(PushStatus.Pending);
    }

    [Fact]
    public void Resolve_NullSettings_DefaultsToTrue()
    {
        var (sendPush, status) = NotificationPushHelper.Resolve(null, NotificationType.Like);

        sendPush.Should().BeTrue();
        status.Should().Be(PushStatus.Pending);
    }

    [Fact]
    public void Resolve_SecurityType_UsesPushSystem()
    {
        var settings = new UserNotificationSettings { PushSystem = false };

        var (sendPush, status) = NotificationPushHelper.Resolve(settings, NotificationType.Security);

        sendPush.Should().BeFalse();
        status.Should().Be(PushStatus.None);
    }
}
