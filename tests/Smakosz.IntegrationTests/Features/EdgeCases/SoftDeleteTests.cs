using Smakosz.Application.Common.Interfaces;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.EdgeCases;

public class SoftDeleteTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            var deletedUser = SeedHelpers.CreateUser(100, "usuniety-user", "usuniety@smakosz.test", hash);
            deletedUser.IsDeleted = true;
            deletedUser.DeletedAt = DateTime.UtcNow.AddDays(-1);

            db.Users.Add(deletedUser);
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task User_SoftDeleted_Returns404()
    {
        var response = await AnonymousClient.GetAsync("/api/users/usuniety-user");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_SoftDeleted_Returns401()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/auth/login", new
        {
            Email = "usuniety@smakosz.test",
            Password = SeedHelpers.DefaultPassword
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
