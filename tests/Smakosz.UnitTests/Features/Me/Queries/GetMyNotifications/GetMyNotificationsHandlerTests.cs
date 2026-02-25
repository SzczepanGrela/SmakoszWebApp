using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Common.Models;
using Smakosz.Application.Features.Me.Queries.GetMyNotifications;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Queries.GetMyNotifications;

[Trait("Category", "Handlers")]
public class GetMyNotificationsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetMyNotificationsHandler _handler;

    public GetMyNotificationsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1, role: "User", sessionId: 100);
        _handler = new GetMyNotificationsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_HappyPath_ReturnsPaginatedNotificationsForCurrentUser()
    {
        _sets.Notifications.Add(new Notification
        {
            NotificationId = 1,
            UserId = 1,
            Type = NotificationType.Like,
            Title = "Like",
            Message = "Someone liked your review",
            IsRead = false,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        _sets.Notifications.Add(new Notification
        {
            NotificationId = 2,
            UserId = 1,
            Type = NotificationType.Follow,
            Title = "Follow",
            Message = "Someone followed you",
            IsRead = true,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        _sets.Notifications.Add(new Notification
        {
            NotificationId = 3,
            UserId = 99,
            Type = NotificationType.System,
            Title = "Other",
            Message = "Other user message",
            IsRead = false,
            PublicId = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new GetMyNotificationsQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Data.Should().HaveCount(2);
        result.Value.Pagination.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsInvalidCredentials()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetMyNotificationsHandler(_db, anonymous);

        var result = await handler.Handle(
            new GetMyNotificationsQuery(new PaginationParams(1, 20)),
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
