using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.MarkNotificationRead;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.MarkNotificationRead;

[Trait("Category", "Handlers")]
public class MarkNotificationReadHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly MarkNotificationReadHandler _handler;
    private static readonly Guid TestPublicId = Guid.NewGuid();

    public MarkNotificationReadHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new MarkNotificationReadHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_SetsIsReadToTrueAndReturnsSuccess()
    {
        var notification = new Notification
        {
            NotificationId = 42,
            UserId = 1,
            Type = NotificationType.System,
            Title = "Test",
            Message = "Hello",
            IsRead = false,
            PublicId = TestPublicId
        };
        _sets.Notifications.Add(notification);
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new MarkNotificationReadCommand(TestPublicId),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        notification.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NotificationNotFound_ReturnsNotFoundError()
    {
        var result = await _handler.Handle(
            new MarkNotificationReadCommand(Guid.NewGuid()),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("NOTIFICATION_NOT_FOUND");
    }
}
