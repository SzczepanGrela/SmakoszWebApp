using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Auth;

public class AccountLockoutTests : IntegrationTestBase
{
    private string _hash = null!;

    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        _hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            db.Users.Add(SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", _hash));
            db.Users.Add(SeedHelpers.CreateLockedUser(11, _hash));
            db.Users.Add(SeedHelpers.CreateLockedUser(12, _hash, DateTime.UtcNow.AddMinutes(-1)));
            var expiredLock = db.Users.Local.First(u => u.UserId == 12);
            expiredLock.Username = "odblokowany";
            expiredLock.Email = "odblokowany@smakosz.test";
            expiredLock.Slug = "odblokowany";
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Login_LockedAccount_Returns403()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "zablokowany@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await DeserializeError(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("AUTH_ACCOUNT_LOCKED");
    }

    [Fact]
    public async Task Login_ExpiredLockout_Returns200()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "odblokowany@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_RepeatedFailures_LocksAccount()
    {
        for (var i = 0; i < 5; i++)
        {
            await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
            {
                Email = "jan@smakosz.test",
                Password = "WrongPassword123!",
                TurnstileToken = "test-token"
            });
        }

        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "jan@smakosz.test",
            Password = SeedHelpers.DefaultPassword,
            TurnstileToken = "test-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await DeserializeError(response);
        error.Should().NotBeNull();
        error!.Code.Should().Be("AUTH_ACCOUNT_LOCKED");
    }

}
