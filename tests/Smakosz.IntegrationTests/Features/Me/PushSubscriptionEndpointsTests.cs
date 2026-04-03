using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Me;

public class PushSubscriptionEndpointsTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<Smakosz.Application.Common.Interfaces.IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash));
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task PostPushSubscription_Authenticated_Returns204()
    {
        var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/me/push-subscriptions", new
        {
            endpoint = "https://fcm.googleapis.com/fcm/send/test-endpoint",
            p256dh = "test-p256dh-key",
            auth = "test-auth-key"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PostPushSubscription_Anonymous_Returns401()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/me/push-subscriptions", new
        {
            endpoint = "https://fcm.googleapis.com/fcm/send/test-endpoint",
            p256dh = "test-p256dh-key",
            auth = "test-auth-key"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostPushSubscription_InvalidData_Returns422()
    {
        var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/me/push-subscriptions", new
        {
            endpoint = "",
            p256dh = "",
            auth = ""
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PostUnsubscribe_Authenticated_Returns204()
    {
        var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/me/push-subscriptions/unsubscribe", new
        {
            endpoint = "https://fcm.googleapis.com/fcm/send/nonexistent"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetPushPublicKey_Authenticated_Returns200()
    {
        var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.GetAsync("/api/me/push-public-key");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
