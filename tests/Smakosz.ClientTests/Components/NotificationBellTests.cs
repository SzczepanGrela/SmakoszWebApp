using Smakosz.ClientTests.Common;

namespace Smakosz.ClientTests.Components;

public class NotificationBellTests : BunitTestBase
{
    [Fact]
    public void UnreadCount_DisplaysBadge()
    {
        var notificationService = Services.GetRequiredService<INotificationService>();
        notificationService.GetUnreadCountAsync().Returns(5);

        var cut = RenderComponent<NotificationBell>();

        cut.WaitForState(() => cut.Markup.Contains("5"));
        cut.Find(".badge.bg-danger").TextContent.Should().Be("5");
    }

    [Fact]
    public void ZeroUnread_NoBadge()
    {
        var notificationService = Services.GetRequiredService<INotificationService>();
        notificationService.GetUnreadCountAsync().Returns(0);

        var cut = RenderComponent<NotificationBell>();

        cut.WaitForAssertion(() =>
            cut.FindAll(".badge.bg-danger").Should().BeEmpty());
    }

    [Fact]
    public void RendersNotificationLink()
    {
        var notificationService = Services.GetRequiredService<INotificationService>();
        notificationService.GetUnreadCountAsync().Returns(0);

        var cut = RenderComponent<NotificationBell>();

        cut.Find("a[href='/notifications']").Should().NotBeNull();
        cut.Find("i.fa-solid.fa-bell").Should().NotBeNull();
    }

    [Fact]
    public void ImplementsIDisposable()
    {
        var notificationService = Services.GetRequiredService<INotificationService>();
        notificationService.GetUnreadCountAsync().Returns(0);

        var cut = RenderComponent<NotificationBell>();
        cut.Dispose();
    }
}
