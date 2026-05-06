using Microsoft.EntityFrameworkCore;
using Npgsql;
using Smakosz.Domain.Entities;
using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Common;

public class RestaurantThemeMigrationTests : IntegrationTestBase
{
    [Fact]
    public async Task RestaurantThemes_UniqueNameConstraint_RejectsDuplicate()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.RestaurantThemes.Add(SeedHelpers.CreateRestaurantTheme(100, "duplicate-name-test", 1));
            db.RestaurantThemes.Add(SeedHelpers.CreateRestaurantTheme(101, "duplicate-name-test", 1));

            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        });
    }

    [Fact]
    public async Task CuisineTypes_UniqueDisplayNameConstraint_RejectsDuplicate()
    {
        await Factory.SeedDataAsync(async db =>
        {
            db.CuisineTypes.Add(new CuisineType { CuisineTypeId = 500, Name = "test-a", DisplayName = "Test Display Unique" });
            db.CuisineTypes.Add(new CuisineType { CuisineTypeId = 501, Name = "test-b", DisplayName = "Test Display Unique" });

            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        });
    }

    [Fact]
    public async Task RestaurantTheme_DeleteCuisineWithThemeReference_Throws()
    {
        await Factory.SeedDataAsync(async db =>
        {
            var cuisine = new CuisineType { CuisineTypeId = 600, Name = "test-delete-protect", DisplayName = "Test Delete Protect" };
            db.CuisineTypes.Add(cuisine);
            await db.SaveChangesAsync();

            db.RestaurantThemes.Add(SeedHelpers.CreateRestaurantTheme(600, "theme-delete-protect", cuisine.CuisineTypeId));
            await db.SaveChangesAsync();

            // Use raw SQL to bypass EF in-memory FK tracking and exercise the DB-level Restrict constraint.
            var act = async () => await db.Database.ExecuteSqlRawAsync("DELETE FROM cuisine_types WHERE cuisine_type_id = 600");
            (await act.Should().ThrowAsync<PostgresException>())
                .Which.SqlState.Should().Be("23503");
        });
    }

    [Fact]
    public async Task Restaurant_DeleteThemeWithRestaurantReference_Throws()
    {
        await Factory.SeedDataAsync(async db =>
        {
            var theme = SeedHelpers.CreateRestaurantTheme(700, "theme-restaurant-protect", 1);
            db.RestaurantThemes.Add(theme);
            await db.SaveChangesAsync();

            var restaurant = SeedHelpers.CreateRestaurant(700, "Test Protect Restaurant");
            restaurant.ThemeId = theme.ThemeId;
            db.Restaurants.Add(restaurant);
            await db.SaveChangesAsync();

            var act = async () => await db.Database.ExecuteSqlRawAsync("DELETE FROM restaurant_themes WHERE theme_id = 700");
            (await act.Should().ThrowAsync<PostgresException>())
                .Which.SqlState.Should().Be("23503");
        });
    }
}
