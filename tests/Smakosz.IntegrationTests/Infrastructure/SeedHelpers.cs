using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence;

namespace Smakosz.IntegrationTests.Infrastructure;

public static class SeedHelpers
{
    public const string DefaultPassword = "Password123!";

    public static City CreateCity(int cityId = 1, string name = "Warszawa", string region = "Mazowieckie")
    {
        return new City
        {
            CityId = cityId,
            CityName = name,
            Region = region,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static User CreateUser(
        int userId = 1,
        string username = "jan-kowalski",
        string email = "jan@smakosz.test",
        string? passwordHash = null)
    {
        return new User
        {
            UserId = userId,
            PublicId = Guid.NewGuid(),
            Username = username,
            Email = email,
            PasswordHash = passwordHash ?? "placeholder-will-be-set",
            EmailVerified = true,
            Role = UserRole.User,
            IsActive = true,
            Slug = username,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static User CreateAdminUser(int userId = 99, string? passwordHash = null)
    {
        return new User
        {
            UserId = userId,
            PublicId = Guid.NewGuid(),
            Username = "administrator",
            Email = "admin@smakosz.test",
            PasswordHash = passwordHash ?? "placeholder-will-be-set",
            EmailVerified = true,
            Role = UserRole.Admin,
            IsActive = true,
            Slug = "administrator",
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static User CreateBusinessUser(int userId = 50, string? passwordHash = null)
    {
        return new User
        {
            UserId = userId,
            PublicId = Guid.NewGuid(),
            Username = "restaurator",
            Email = "restaurator@smakosz.test",
            PasswordHash = passwordHash ?? "placeholder-will-be-set",
            EmailVerified = true,
            Role = UserRole.Restaurant,
            IsActive = true,
            Slug = "restaurator",
            RestaurantId = null,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static User CreateBannedUser(int userId = 10, string? passwordHash = null)
    {
        return new User
        {
            UserId = userId,
            PublicId = Guid.NewGuid(),
            Username = "zbanowany",
            Email = "zbanowany@smakosz.test",
            PasswordHash = passwordHash ?? "placeholder-will-be-set",
            EmailVerified = true,
            Role = UserRole.User,
            IsActive = true,
            IsBanned = true,
            Slug = "zbanowany",
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static Restaurant CreateRestaurant(
        int restaurantId = 1,
        string name = "Pizzeria Roma",
        int? cityId = 1,
        int? ownerId = null)
    {
        return new Restaurant
        {
            RestaurantId = restaurantId,
            PublicId = Guid.NewGuid(),
            RestaurantName = name,
            Slug = name.ToLower().Replace(" ", "-"),
            CuisineType = "Włoska",
            PriceLevel = 2,
            Address = "ul. Marszalkowska 10",
            CityId = cityId,
            OwnerId = ownerId,
            Status = RestaurantStatus.Active,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static Dish CreateDish(
        int dishId = 1,
        string name = "Pizza Margherita",
        int restaurantId = 1,
        decimal price = 24.90m)
    {
        return new Dish
        {
            DishId = dishId,
            PublicId = Guid.NewGuid(),
            DishName = name,
            Slug = name.ToLower().Replace(" ", "-"),
            RestaurantId = restaurantId,
            Price = price,
            Calories = 850,
            AvgRating = 8.0,
            ReviewCount = 0,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static Review CreateReview(
        int reviewId = 1,
        int userId = 1,
        int dishId = 1,
        int restaurantId = 1)
    {
        return new Review
        {
            ReviewId = reviewId,
            PublicId = Guid.NewGuid(),
            UserId = userId,
            DishId = dishId,
            RestaurantId = restaurantId,
            DishRating = 8,
            ServiceRating = 7,
            CleanlinessRating = 8,
            AmbianceRating = 7,
            Content = "Swietna pizza, ciasto idealne!",
            ModerationStatus = ContentModerationStatus.Approved,
            IsVisible = true,
            IsApproved = true,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static Ingredient CreateIngredient(
        int ingredientId = 1,
        string name = "ser mozzarella")
    {
        return new Ingredient
        {
            IngredientId = ingredientId,
            IngredientName = name,
            IsAllergen = true,
            IsVegetarian = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static Domain.Entities.System.SiteStats CreateSiteStats() => new()
    {
        Id = 1,
        TotalDishes = 0,
        TotalRestaurants = 0,
        TotalReviews = 0,
        TotalUsers = 0,
        UpdatedAt = DateTime.UtcNow,
    };

    public static ReportReasonDefinition CreateReportReason(
        string code = "spam",
        string label = "Spam lub reklama")
    {
        return new ReportReasonDefinition
        {
            ReasonCode = code,
            LabelPl = label,
            SeverityScore = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static MenuSection CreateMenuSection(
        int sectionId = 1,
        int restaurantId = 1,
        string name = "Pizze",
        int displayOrder = 1)
    {
        return new MenuSection
        {
            SectionId = sectionId,
            RestaurantId = restaurantId,
            SectionName = name,
            DisplayOrder = displayOrder,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static Domain.Entities.System.ForbiddenWord CreateForbiddenWord(
        int wordId,
        string word,
        ForbiddenWordCategory category = ForbiddenWordCategory.Profanity,
        bool isRegex = false)
    {
        return new Domain.Entities.System.ForbiddenWord
        {
            WordId = wordId,
            Word = word,
            Category = category,
            IsRegex = isRegex,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static void SeedForbiddenWords(SmakoszDbContext db)
    {
        db.ForbiddenWords.AddRange(
            CreateForbiddenWord(1, "kurwa", ForbiddenWordCategory.Profanity),
            CreateForbiddenWord(2, "chuj", ForbiddenWordCategory.Profanity),
            CreateForbiddenWord(3, "jebane", ForbiddenWordCategory.Profanity),
            CreateForbiddenWord(4, "fuck", ForbiddenWordCategory.Offensive),
            CreateForbiddenWord(5, "shit", ForbiddenWordCategory.Offensive)
        );
    }

    public static async Task SeedRestaurantScenarioAsync(SmakoszDbContext db, string? passwordHash = null)
    {
        var city = CreateCity(1, "Warszawa");
        var user = CreateUser(1, "jan-kowalski", "jan@smakosz.test", passwordHash);
        var restaurant = CreateRestaurant(1, "Pizzeria Roma", city.CityId);
        var dish1 = CreateDish(1, "Pizza Margherita", restaurant.RestaurantId, 24.90m);
        var dish2 = CreateDish(2, "Pizza Pepperoni", restaurant.RestaurantId, 29.90m);

        db.Cities.Add(city);
        db.Users.Add(user);
        db.Restaurants.Add(restaurant);
        db.Dishes.AddRange(dish1, dish2);

        await db.SaveChangesAsync();
    }

    public static async Task SeedPublicEndpointsScenarioAsync(SmakoszDbContext db, string? passwordHash = null)
    {
        var warszawa = CreateCity(1, "Warszawa");
        var krakow = CreateCity(2, "Krakow", "Malopolskie");

        var user1 = CreateUser(1, "jan-kowalski", "jan@smakosz.test", passwordHash);
        var user2 = CreateUser(2, "anna-nowak", "anna@smakosz.test", passwordHash);

        var pizzeria = CreateRestaurant(1, "Pizzeria Roma", warszawa.CityId);
        var kebab = CreateRestaurant(2, "Sultan Kebab", krakow.CityId);
        kebab.CuisineType = "Turecka";
        kebab.PriceLevel = 1;

        var dish1 = CreateDish(1, "Pizza Margherita", pizzeria.RestaurantId, 24.90m);
        var dish2 = CreateDish(2, "Pizza Pepperoni", pizzeria.RestaurantId, 29.90m);
        var dish3 = CreateDish(3, "Kebab Duzy", kebab.RestaurantId, 22.00m);

        var review1 = CreateReview(1, user1.UserId, dish1.DishId, pizzeria.RestaurantId);
        var review2 = CreateReview(2, user2.UserId, dish3.DishId, kebab.RestaurantId);

        db.Cities.AddRange(warszawa, krakow);
        db.Users.AddRange(user1, user2);
        db.Restaurants.AddRange(pizzeria, kebab);
        db.Dishes.AddRange(dish1, dish2, dish3);
        db.Reviews.AddRange(review1, review2);

        await db.SaveChangesAsync();
    }
}
