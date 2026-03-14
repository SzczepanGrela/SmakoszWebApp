using FluentAssertions;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Application.Features.Me.Commands.RemovePushSubscription;
using Smakosz.UnitTests.Common.TestInfrastructure;

namespace Smakosz.UnitTests.Features.Me.Commands.RemovePushSubscription;

[Trait("Category", "Handlers")]
public class RemovePushSubscriptionHandlerTests
{
    private readonly ISmakoszDbContext _db;
    private readonly MockDbSets _sets;
    private readonly RemovePushSubscriptionHandler _handler;

    public RemovePushSubscriptionHandlerTests()
    {
        (_db, _sets) = DbContextMockFactory.Create();
        var currentUser = MockExtensions.CreateAuthenticatedUser(userId: 1);
        _handler = new RemovePushSubscriptionHandler(_db, currentUser);
    }

    [Fact]
    public async Task Handle_ExistingSubscription_RemovesIt()
    {
        _sets.PushSubscriptions.Add(new Domain.Entities.PushSubscription
        {
            PushSubscriptionId = 1,
            UserId = 1,
            Endpoint = "https://push.example.com/sub1",
            P256dh = "p256dh",
            Auth = "auth",
            CreatedAt = DateTime.UtcNow
        });
        DbContextMockFactory.Refresh(_db, _sets);

        var command = new RemovePushSubscriptionCommand("https://push.example.com/sub1");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
        _sets.PushSubscriptions.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NonExistingSubscription_SucceedsIdempotent()
    {
        var command = new RemovePushSubscriptionCommand("https://push.example.com/nonexistent");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsError.Should().BeFalse();
    }
}
