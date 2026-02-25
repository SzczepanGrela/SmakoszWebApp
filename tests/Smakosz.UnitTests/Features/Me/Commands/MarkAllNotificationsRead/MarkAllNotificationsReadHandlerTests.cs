using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.MarkAllNotificationsRead;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.MarkAllNotificationsRead;

[Trait("Category", "Handlers")]
public class MarkAllNotificationsReadHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly MarkAllNotificationsReadHandler _handler;

    public MarkAllNotificationsReadHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new MarkAllNotificationsReadHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_MarksAllUnreadNotificationsAsRead()
    {
        _sets.Notifications.Add(new Notification
        {
            NotificationId = 1,
            UserId = 1,
            Type = NotificationType.System,
            Title = "T1",
            Message = "M1",
            IsRead = false,
            PublicId = Guid.NewGuid()
        });
        _sets.Notifications.Add(new Notification
        {
            NotificationId = 2,
            UserId = 1,
            Type = NotificationType.Like,
            Title = "T2",
            Message = "M2",
            IsRead = false,
            PublicId = Guid.NewGuid()
        });
        _sets.Notifications.Add(new Notification
        {
            NotificationId = 3,
            UserId = 2,
            Type = NotificationType.Follow,
            Title = "T3",
            Message = "M3",
            IsRead = false,
            PublicId = Guid.NewGuid()
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new MarkAllNotificationsReadCommand(),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.Notifications.Where(n => n.UserId == 1).All(n => n.IsRead).Should().BeTrue();
        _sets.Notifications.First(n => n.NotificationId == 3).IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoUnreadNotifications_StillReturnsSuccess()
    {
        _sets.Notifications.Add(new Notification
        {
            NotificationId = 1,
            UserId = 1,
            Type = NotificationType.System,
            Title = "T1",
            Message = "M1",
            IsRead = true,
            PublicId = Guid.NewGuid()
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new MarkAllNotificationsReadCommand(),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
    }
}
