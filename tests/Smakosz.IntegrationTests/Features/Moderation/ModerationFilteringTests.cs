using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Enums;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Moderation;

public class ModerationFilteringTests : IntegrationTestBase
{
    protected override async Task SeedAsync()
    {
        var hasher = Factory.GetService<IPasswordHasher>();
        var hash = hasher.Hash(SeedHelpers.DefaultPassword);

        await Factory.SeedDataAsync(async db =>
        {
            var city = SeedHelpers.CreateCity(1, "Warszawa");
            var user = SeedHelpers.CreateUser(1, "jan-kowalski", "jan@smakosz.test", hash);
            var businessUser = SeedHelpers.CreateBusinessUser(50, hash);
            var restaurant = SeedHelpers.CreateRestaurant(1, "Pizzeria Roma", city.CityId, ownerId: 50);

            var dishApproved = SeedHelpers.CreateDish(1, "Pizza Approved", restaurant.RestaurantId, 24.90m);
            dishApproved.ModerationStatus = ContentModerationStatus.Approved;
            dishApproved.Slug = "pizza-approved";

            var dishNone = SeedHelpers.CreateDish(2, "Pizza None", restaurant.RestaurantId, 22.00m);
            dishNone.ModerationStatus = ContentModerationStatus.None;
            dishNone.Slug = "pizza-none";

            var dishPending = SeedHelpers.CreateDish(3, "Pizza Pending", restaurant.RestaurantId, 28.00m);
            dishPending.ModerationStatus = ContentModerationStatus.Pending;
            dishPending.Slug = "pizza-pending";

            var sectionApproved = SeedHelpers.CreateMenuSection(1, restaurant.RestaurantId, "Approved Section");
            sectionApproved.ModerationStatus = ContentModerationStatus.Approved;

            var sectionPending = SeedHelpers.CreateMenuSection(2, restaurant.RestaurantId, "Pending Section", displayOrder: 2);
            sectionPending.ModerationStatus = ContentModerationStatus.Pending;

            db.Cities.Add(city);
            db.Users.AddRange(user, businessUser);
            db.Restaurants.Add(restaurant);
            db.Dishes.AddRange(dishApproved, dishNone, dishPending);
            db.MenuSections.AddRange(sectionApproved, sectionPending);
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetDishes_FiltersPendingDishes()
    {
        var response = await AnonymousClient.GetAsync("/api/restaurants/pizzeria-roma/dishes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Pizza Approved");
        body.Should().Contain("Pizza None");
        body.Should().NotContain("Pizza Pending");
    }

    [Fact]
    public async Task GetDishBySlug_PendingDish_Returns404()
    {
        var response = await AnonymousClient.GetAsync("/api/dishes/pizza-pending");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDishBySlug_ApprovedDish_Returns200()
    {
        var response = await AnonymousClient.GetAsync("/api/dishes/pizza-approved");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Pizza Approved");
    }

    [Fact]
    public async Task GetRestaurantDetail_FiltersPendingMenuSections()
    {
        var response = await AnonymousClient.GetAsync("/api/restaurants/pizzeria-roma");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Approved Section");
        body.Should().NotContain("Pending Section");
    }

    [Fact]
    public async Task BusinessPanel_SeesPendingDishes()
    {
        using var client = Factory.CreateBusinessClient(50, "restaurator");

        var response = await client.GetAsync("/api/business/dishes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Pizza Approved");
        body.Should().Contain("Pizza Pending");
    }
}
