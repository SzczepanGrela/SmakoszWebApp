using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Queries.GetUnreadNotificationCount;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Queries.GetUnreadNotificationCount;

[Trait("Category", "Handlers")]
public class GetUnreadNotificationCountHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetUnreadNotificationCountHandler _handler;

    public GetUnreadNotificationCountHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new GetUnreadNotificationCountHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ReturnsCount()
    {
        _sets.Notifications.Add(new Notification { NotificationId = 1, UserId = 1, IsRead = false, Type = NotificationType.System, Title = "A", Message = "B" });
        _sets.Notifications.Add(new Notification { NotificationId = 2, UserId = 1, IsRead = true, Type = NotificationType.System, Title = "C", Message = "D" });
        _sets.Notifications.Add(new Notification { NotificationId = 3, UserId = 1, IsRead = false, Type = NotificationType.System, Title = "E", Message = "F" });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetUnreadNotificationCountQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.Should().Be(2);
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetUnreadNotificationCountHandler(_db, anonymous);

        var result = await handler.Handle(new GetUnreadNotificationCountQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
