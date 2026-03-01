using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.UpdateNotificationSettings;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.UpdateNotificationSettings;

[Trait("Category", "Handlers")]
public class UpdateNotificationSettingsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly UpdateNotificationSettingsHandler _handler;

    public UpdateNotificationSettingsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new UpdateNotificationSettingsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ExistingSettings_UpdatesFlags()
    {
        _sets.NotificationSettings.Add(new UserNotificationSettings
        {
            UserId = 1, PushLike = true, PushFollow = true, PushSystem = true
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(
            new UpdateNotificationSettingsCommand(false, false, true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.NotificationSettings[0].PushLike.Should().BeFalse();
        _sets.NotificationSettings[0].PushFollow.Should().BeFalse();
        _sets.NotificationSettings[0].PushSystem.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoSettings_CreatesNew()
    {
        var result = await _handler.Handle(
            new UpdateNotificationSettingsCommand(true, false, true), CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.NotificationSettings.Should().HaveCount(1);
        _sets.NotificationSettings[0].PushFollow.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new UpdateNotificationSettingsHandler(_db, anonymous);

        var result = await handler.Handle(
            new UpdateNotificationSettingsCommand(true, true, true), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
