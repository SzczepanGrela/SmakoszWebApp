using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.SavePushSubscription;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.SavePushSubscription;

[Trait("Category", "Handlers")]
public class SavePushSubscriptionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly SavePushSubscriptionHandler _handler;

    public SavePushSubscriptionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new SavePushSubscriptionHandler(_db, currentUser);
    }

    [Fact]
    public async Task Handle_NewSubscription_AddsToDb()
    {
        var command = new SavePushSubscriptionCommand("https://push.example.com/sub1", "p256dh-key", "auth-key");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.PushSubscriptions.Should().HaveCount(1);
        _sets.PushSubscriptions[0].Endpoint.Should().Be("https://push.example.com/sub1");
        _sets.PushSubscriptions[0].UserId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExistingEndpoint_UpdatesKeys()
    {
        _sets.PushSubscriptions.Add(new Domain.Entities.PushSubscription
        {
            PushSubscriptionId = 1,
            UserId = 2,
            Endpoint = "https://push.example.com/sub1",
            P256dh = "old-p256dh",
            Auth = "old-auth",
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var command = new SavePushSubscriptionCommand("https://push.example.com/sub1", "new-p256dh", "new-auth");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.PushSubscriptions[0].P256dh.Should().Be("new-p256dh");
        _sets.PushSubscriptions[0].Auth.Should().Be("new-auth");
        _sets.PushSubscriptions[0].UserId.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Unauthenticated_ReturnsError()
    {
        var (db, _) = DbContextMockFactory.Create();
        var anonUser = MockExtensions.CreateAnonymousUser();
        var handler = new SavePushSubscriptionHandler(db, anonUser);

        var command = new SavePushSubscriptionCommand("https://push.example.com/sub1", "p256dh", "auth");

        var result = await handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeTrue();
    }
}
