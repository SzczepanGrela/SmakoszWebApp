using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Queries.GetNotificationSettings;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Queries.GetNotificationSettings;

[Trait("Category", "Handlers")]
public class GetNotificationSettingsHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly ICurrentUserService _currentUser;
    private readonly GetNotificationSettingsHandler _handler;

    public GetNotificationSettingsHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        _currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new GetNotificationSettingsHandler(_db, _currentUser);
    }

    [Fact]
    public async Task Handle_ExistingSettings_ReturnsSettings()
    {
        _sets.NotificationSettings.Add(new UserNotificationSettings
        {
            UserId = 1, PushLike = false, PushFollow = true, PushSystem = false
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var result = await _handler.Handle(new GetNotificationSettingsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.PushLike.Should().BeFalse();
        result.Value.PushFollow.Should().BeTrue();
        result.Value.PushSystem.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_NoSettings_ReturnsDefaults()
    {
        var result = await _handler.Handle(new GetNotificationSettingsQuery(), CancellationToken.None);

        result.IsError.Should().BeFalse();
        result.Value.PushLike.Should().BeTrue();
        result.Value.PushFollow.Should().BeTrue();
        result.Value.PushSystem.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NotAuthenticated_ReturnsError()
    {
        var anonymous = MockExtensions.CreateAnonymousUser();
        var handler = new GetNotificationSettingsHandler(_db, anonymous);

        var result = await handler.Handle(new GetNotificationSettingsQuery(), CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.FirstError.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }
}
