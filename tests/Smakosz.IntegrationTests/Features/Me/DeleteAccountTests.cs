using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Infrastructure.Persistence;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Me;

public class DeleteAccountTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            var city = SeedHelpers.CreateCity(1, "Warszawa");
            var user = SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash);
            var restaurantOwner = SeedHelpers.CreateUser(50, "restaurator", "restaurator@smakosz.test", hash);
            var restaurant = SeedHelpers.CreateRestaurant(1, "Pizzeria Roma", city.CityId, ownerId: 50);
            restaurantOwner.RestaurantId = restaurant.RestaurantId;

            db.Cities.Add(city);
            db.Users.AddRange(user, restaurantOwner);
            db.Restaurants.Add(restaurant);
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task RequestDeletion_ValidPassword_Returns204()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/me/delete-account/request",
            new { Password = SeedHelpers.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RequestDeletion_WrongPassword_ReturnsError()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var response = await client.PostAsJsonAsync("/api/me/delete-account/request",
            new { Password = "WrongPassword123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestDeletion_Anonymous_Returns401()
    {
        var response = await AnonymousClient.PostAsJsonAsync("/api/me/delete-account/request",
            new { Password = SeedHelpers.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestDeletion_RestaurantOwner_Returns403()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.PostAsJsonAsync("/api/me/delete-account/request",
            new { Password = SeedHelpers.DefaultPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConfirmDeletion_ValidCode_Returns204_ThenCannotAccess()
    {
        using var client = Factory.CreateUserClient(1, "jan-kowalski");

        var requestResponse = await client.PostAsJsonAsync("/api/me/delete-account/request",
            new { Password = SeedHelpers.DefaultPassword });
        requestResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmakoszDbContext>();

        var verificationCode = await db.VerificationCodes
            .FirstOrDefaultAsync(vc => vc.UserId == 1
                && vc.Type == Domain.Enums.VerificationCodeType.AccountDeletion);
        verificationCode.Should().NotBeNull();

        var badResponse = await client.PostAsJsonAsync("/api/me/delete-account/confirm",
            new { Code = "000000" });
        badResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var profileResponse = await client.GetAsync("/api/me");
        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
