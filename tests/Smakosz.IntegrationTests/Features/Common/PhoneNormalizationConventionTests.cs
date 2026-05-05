using Smakosz.IntegrationTests.Infrastructure;

namespace Smakosz.IntegrationTests.Features.Common;

public class PhoneNormalizationConventionTests : IntegrationTestBase
{
    [Fact]
    public async Task SaveChanges_NormalizesUserPhoneOnInsert()
    {
        await Factory.SeedDataAsync(async db =>
        {
            var user = SeedHelpers.CreateUser(1, "tester", "tester@smakosz.test");
            user.Phone = "+48 111 222 333";
            db.Users.Add(user);
            await db.SaveChangesAsync();

            user.Phone.Should().Be("+48111222333");
        });
    }

    [Fact]
    public async Task SaveChanges_NormalizesUserPhoneOnUpdate()
    {
        await Factory.SeedDataAsync(async db =>
        {
            var user = SeedHelpers.CreateUser(1, "tester", "tester@smakosz.test");
            user.Phone = "+48111222333";
            db.Users.Add(user);
            await db.SaveChangesAsync();

            user.Phone = "+48 999 888 777";
            await db.SaveChangesAsync();

            user.Phone.Should().Be("+48999888777");
        });
    }

    [Fact]
    public async Task SaveChanges_NormalizesRestaurantPhoneOnInsert()
    {
        await Factory.SeedDataAsync(async db =>
        {
            var restaurant = SeedHelpers.CreateRestaurant(1, "Test");
            restaurant.Phone = "0048-123-456-789";
            db.Restaurants.Add(restaurant);
            await db.SaveChangesAsync();

            restaurant.Phone.Should().Be("+48123456789");
        });
    }

    [Fact]
    public async Task SaveChanges_NormalizesNineDigitPhoneAddingPlus48()
    {
        await Factory.SeedDataAsync(async db =>
        {
            var user = SeedHelpers.CreateUser(1, "tester", "tester@smakosz.test");
            user.Phone = "123456789";
            db.Users.Add(user);
            await db.SaveChangesAsync();

            user.Phone.Should().Be("+48123456789");
        });
    }

    [Fact]
    public async Task SaveChanges_LeavesNullPhoneUntouched()
    {
        await Factory.SeedDataAsync(async db =>
        {
            var user = SeedHelpers.CreateUser(1, "tester", "tester@smakosz.test");
            user.Phone = null;
            db.Users.Add(user);
            await db.SaveChangesAsync();

            user.Phone.Should().BeNull();
        });
    }

    [Fact]
    public async Task SaveChanges_RejectsInvalidPhoneFormat()
    {
        await Factory.SeedDataAsync(async db =>
        {
            var user = SeedHelpers.CreateUser(1, "tester", "tester@smakosz.test");
            user.Phone = "abc";
            db.Users.Add(user);

            var act = () => db.SaveChangesAsync();
            await act.Should().ThrowAsync<ArgumentException>()
                .WithMessage("*Nieprawidlowy format numeru telefonu*");
        });
    }
}
