using Microsoft.EntityFrameworkCore;
using Smakosz.Domain.Entities;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;
using Smakosz.Infrastructure.Persistence;
using Smakosz.Infrastructure.Services;

namespace Smakosz.E2E.SeedData;

public static class SeedData
{
    private static string HashPassword(string password)
    {
        return new PasswordHasher().Hash(password);
    }

    public static async Task SeedAsync(SmakoszDbContext db)
    {
        var passwordHash = HashPassword("TestHaslo123!");

        var warszawa = new City
        {
            CityId = 1,
            CityName = "Warszawa",
            Region = "Mazowieckie",
            CreatedAt = DateTime.UtcNow,
        };
        var krakow = new City
        {
            CityId = 2,
            CityName = "Krakow",
            Region = "Malopolskie",
            CreatedAt = DateTime.UtcNow,
        };
        var wroclaw = new City
        {
            CityId = 3,
            CityName = "Wroclaw",
            Region = "Dolnoslaskie",
            CreatedAt = DateTime.UtcNow,
        };
        db.Cities.AddRange(warszawa, krakow, wroclaw);

        db.CuisineTypes.AddRange(
            new CuisineType { CuisineTypeId = 1001, Name = "Włoska", DisplayName = "Włoska" },
            new CuisineType { CuisineTypeId = 1002, Name = "Turecka", DisplayName = "Turecka" },
            new CuisineType { CuisineTypeId = 1003, Name = "Polska", DisplayName = "Polska" });

        var jan = new User
        {
            UserId = 1,
            PublicId = Guid.NewGuid(),
            Username = "jan-kowalski",
            Email = "jan.kowalski@gmail.com",
            PasswordHash = passwordHash,
            FirstName = "Jan",
            LastName = "Kowalski",
            EmailVerified = true,
            Role = UserRole.User,
            IsActive = true,
            Slug = "jan-kowalski",
            CreatedAt = DateTime.UtcNow,
        };
        var anna = new User
        {
            UserId = 2,
            PublicId = Guid.NewGuid(),
            Username = "anna-nowak",
            Email = "anna.nowak@wp.pl",
            PasswordHash = passwordHash,
            FirstName = "Anna",
            LastName = "Nowak",
            EmailVerified = true,
            Role = UserRole.User,
            IsActive = true,
            Slug = "anna-nowak",
            CreatedAt = DateTime.UtcNow,
        };
        var marco = new User
        {
            UserId = 3,
            PublicId = Guid.NewGuid(),
            Username = "marco-rossi",
            Email = "marco.rossi@pizzeriaroma.pl",
            PasswordHash = passwordHash,
            FirstName = "Marco",
            LastName = "Rossi",
            EmailVerified = true,
            Role = UserRole.Restaurant,
            IsActive = true,
            Slug = "marco-rossi",
            CreatedAt = DateTime.UtcNow,
        };
        var admin = new User
        {
            UserId = 4,
            PublicId = Guid.NewGuid(),
            Username = "administrator",
            Email = "admin@smakosz.pl",
            PasswordHash = passwordHash,
            FirstName = "Admin",
            LastName = "Smakosz",
            EmailVerified = true,
            Role = UserRole.Admin,
            IsActive = true,
            Slug = "administrator",
            CreatedAt = DateTime.UtcNow,
        };
        var banned = new User
        {
            UserId = 5,
            PublicId = Guid.NewGuid(),
            Username = "zbanowany",
            Email = "zbanowany@smakosz.test",
            PasswordHash = passwordHash,
            FirstName = "Zbanowany",
            LastName = "Testowy",
            EmailVerified = true,
            Role = UserRole.User,
            IsActive = true,
            IsBanned = true,
            Slug = "zbanowany",
            CreatedAt = DateTime.UtcNow,
        };
        var moderator = new User
        {
            UserId = 6,
            PublicId = Guid.NewGuid(),
            Username = "moderator",
            Email = "moderator@smakosz.test",
            PasswordHash = passwordHash,
            FirstName = "Mod",
            LastName = "Erator",
            EmailVerified = true,
            Role = UserRole.Moderator,
            IsActive = true,
            Slug = "moderator",
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.AddRange(jan, anna, marco, admin, banned, moderator);

        var pizzeriaRoma = new Restaurant
        {
            RestaurantId = 1,
            PublicId = Guid.NewGuid(),
            RestaurantName = "Pizzeria Roma",
            Slug = "pizzeria-roma",
            CuisineTypeId = 1001,
            PriceLevel = 2,
            Address = "ul. Marszalkowska 10",
            CityId = 1,
            OwnerId = 3,
            Status = RestaurantStatus.Active,
            IsVerified = true,
            AvgService = 7.5,
            AvgCleanliness = 8.0,
            AvgAmbiance = 7.5,
            CreatedAt = DateTime.UtcNow,
        };
        var sultanKebab = new Restaurant
        {
            RestaurantId = 2,
            PublicId = Guid.NewGuid(),
            RestaurantName = "Sultan Kebab",
            Slug = "sultan-kebab",
            CuisineTypeId = 1002,
            PriceLevel = 1,
            Address = "ul. Grodzka 5",
            CityId = 2,
            Status = RestaurantStatus.Active,
            IsVerified = true,
            AvgService = 7.0,
            AvgCleanliness = 7.0,
            AvgAmbiance = 7.0,
            CreatedAt = DateTime.UtcNow,
        };
        var nowaRestauracja = new Restaurant
        {
            RestaurantId = 3,
            PublicId = Guid.NewGuid(),
            RestaurantName = "Nowa Restauracja",
            Slug = "nowa-restauracja",
            CuisineTypeId = 1003,
            PriceLevel = 2,
            Address = "ul. Swidnicka 22",
            CityId = 3,
            Status = RestaurantStatus.PendingVerification,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Restaurants.AddRange(pizzeriaRoma, sultanKebab, nowaRestauracja);

        var margherita = new Dish
        {
            DishId = 1,
            PublicId = Guid.NewGuid(),
            DishName = "Pizza Margherita",
            Slug = "pizza-margherita",
            RestaurantId = 1,
            Price = 24.90m,
            Calories = 850,
            Description = "Klasyczna wloska pizza z sosem pomidorowym, mozzarella i bazylia.",
            IsVegetarian = true,
            IsAvailable = true,
            AvgRating = 8.5,
            ReviewCount = 2,
            CreatedAt = DateTime.UtcNow,
        };
        var pepperoni = new Dish
        {
            DishId = 2,
            PublicId = Guid.NewGuid(),
            DishName = "Pizza Pepperoni",
            Slug = "pizza-pepperoni",
            RestaurantId = 1,
            Price = 29.90m,
            Calories = 1020,
            Description = "Pizza z pikantnym pepperoni, mozzarella i sosem pomidorowym.",
            IsAvailable = true,
            AvgRating = 8.0,
            ReviewCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        var kebabDuzy = new Dish
        {
            DishId = 3,
            PublicId = Guid.NewGuid(),
            DishName = "Kebab Duzy",
            Slug = "kebab-duzy",
            RestaurantId = 2,
            Price = 22.00m,
            Calories = 780,
            Description = "Duzy kebab w ciescie z miesem, surowkami i sosami.",
            IsAvailable = true,
            AvgRating = 8.0,
            ReviewCount = 1,
            CreatedAt = DateTime.UtcNow,
        };
        var tiramisu = new Dish
        {
            DishId = 4,
            PublicId = Guid.NewGuid(),
            DishName = "Tiramisu",
            Slug = "tiramisu",
            RestaurantId = 1,
            Price = 18.50m,
            Calories = 450,
            Description = "Domowe tiramisu z mascarpone, espresso i amaretto.",
            IsVegetarian = true,
            IsAvailable = true,
            AvgRating = 9.0,
            ReviewCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        var pendingDish = new Dish
        {
            DishId = 5,
            PublicId = Guid.NewGuid(),
            DishName = "Pizza Testowa Pending",
            Slug = "pizza-testowa-pending",
            RestaurantId = 1,
            Price = 32.00m,
            Calories = 900,
            Description = "Danie oczekujace na moderacje.",
            IsAvailable = true,
            ModerationStatus = ContentModerationStatus.Pending,
            AvgRating = 0,
            ReviewCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        db.Dishes.AddRange(margherita, pepperoni, kebabDuzy, tiramisu, pendingDish);

        var tagNaWynos = new Tag { TagId = 1001, TagName = "Na wynos", Category = "feature", DisplayColor = "#28a745", CreatedAt = DateTime.UtcNow };
        var tagSezonowe = new Tag { TagId = 1002, TagName = "Sezonowe", Category = "feature", DisplayColor = "#fd7e14", CreatedAt = DateTime.UtcNow };
        var tagNowosc = new Tag { TagId = 1003, TagName = "Nowosc", Category = "feature", DisplayColor = "#007bff", CreatedAt = DateTime.UtcNow };
        db.Tags.AddRange(tagNaWynos, tagSezonowe, tagNowosc);

        db.DishTags.AddRange(
            new DishTag { DishId = 1, TagId = 1001 }, // Margherita -> Na wynos
            new DishTag { DishId = 1, TagId = 1002 }, // Margherita -> Sezonowe
            new DishTag { DishId = 3, TagId = 1001 }, // Kebab -> Na wynos
            new DishTag { DishId = 4, TagId = 1003 }); // Tiramisu -> Nowosc

        await db.SaveChangesAsync();

        var dishCategoryByName = await db.Tags
            .Where(t => t.Category == "dish_category")
            .ToDictionaryAsync(t => t.TagName, t => t.TagId);

        void LinkCategory(int dishId, string categoryName)
        {
            if (dishCategoryByName.TryGetValue(categoryName, out var tagId))
                db.DishTags.Add(new DishTag { DishId = dishId, TagId = tagId });
        }

        LinkCategory(1, "Pizza"); // Margherita
        LinkCategory(2, "Pizza"); // Pepperoni
        LinkCategory(3, "Kebab"); // Kebab Duzy
        LinkCategory(4, "Deser"); // Tiramisu
        LinkCategory(5, "Pizza"); // Pending Pizza

        var maka = new Ingredient { IngredientId = 1, IngredientName = "Maka pszenna", IsAllergen = true, IsGlutenFree = false, CreatedAt = DateTime.UtcNow };
        var mozzarella = new Ingredient { IngredientId = 2, IngredientName = "Ser mozzarella", IsAllergen = true, IsLactoseFree = false, CreatedAt = DateTime.UtcNow };
        var sosPomidorowy = new Ingredient { IngredientId = 3, IngredientName = "Sos pomidorowy", CreatedAt = DateTime.UtcNow };
        var bazylia = new Ingredient { IngredientId = 4, IngredientName = "Bazylia", CreatedAt = DateTime.UtcNow };
        var miesoWolowe = new Ingredient { IngredientId = 5, IngredientName = "Mieso wolowe", IsVegetarian = false, IsVegan = false, CreatedAt = DateTime.UtcNow };
        db.Ingredients.AddRange(maka, mozzarella, sosPomidorowy, bazylia, miesoWolowe);

        db.DishIngredients.AddRange(
            new DishIngredient { DishId = 1, IngredientId = 1 }, // Margherita -> Maka
            new DishIngredient { DishId = 1, IngredientId = 2 }, // Margherita -> Mozzarella
            new DishIngredient { DishId = 1, IngredientId = 3 }, // Margherita -> Sos pomidorowy
            new DishIngredient { DishId = 1, IngredientId = 4 }, // Margherita -> Bazylia
            new DishIngredient { DishId = 3, IngredientId = 5 }, // Kebab -> Mieso wolowe
            new DishIngredient { DishId = 3, IngredientId = 1 }); // Kebab -> Maka

        db.ForbiddenWords.AddRange(
            new ForbiddenWord { WordId = 1, Word = "kurwa", Category = ForbiddenWordCategory.Profanity, CreatedAt = DateTime.UtcNow },
            new ForbiddenWord { WordId = 2, Word = "chuj", Category = ForbiddenWordCategory.Profanity, CreatedAt = DateTime.UtcNow },
            new ForbiddenWord { WordId = 3, Word = "jebane", Category = ForbiddenWordCategory.Profanity, CreatedAt = DateTime.UtcNow },
            new ForbiddenWord { WordId = 4, Word = "fuck", Category = ForbiddenWordCategory.Offensive, CreatedAt = DateTime.UtcNow },
            new ForbiddenWord { WordId = 5, Word = "shit", Category = ForbiddenWordCategory.Offensive, CreatedAt = DateTime.UtcNow });

        db.MenuSections.AddRange(
            new MenuSection { SectionId = 1, RestaurantId = 1, SectionName = "Pizze", DisplayOrder = 1, ModerationStatus = ContentModerationStatus.Approved, CreatedAt = DateTime.UtcNow },
            new MenuSection { SectionId = 2, RestaurantId = 1, SectionName = "Desery", DisplayOrder = 2, ModerationStatus = ContentModerationStatus.Approved, CreatedAt = DateTime.UtcNow },
            new MenuSection { SectionId = 3, RestaurantId = 1, SectionName = "Sekcja Pending", DisplayOrder = 3, ModerationStatus = ContentModerationStatus.Pending, CreatedAt = DateTime.UtcNow });

        var review1 = new Review
        {
            ReviewId = 1,
            PublicId = Guid.NewGuid(),
            UserId = 1,
            DishId = 1,
            RestaurantId = 1,
            DishRating = 9,
            ServiceRating = 8,
            CleanlinessRating = 8,
            AmbianceRating = 8,
            Content = "Swietna pizza, ciasto idealne! Obsluga szybka i mila.",
            ModerationStatus = ContentModerationStatus.Approved,
            IsVisible = true,
            IsApproved = true,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            CreatedAt = DateTime.UtcNow,
        };
        var review2 = new Review
        {
            ReviewId = 2,
            PublicId = Guid.NewGuid(),
            UserId = 2,
            DishId = 1,
            RestaurantId = 1,
            DishRating = 8,
            ServiceRating = 7,
            CleanlinessRating = 8,
            AmbianceRating = 7,
            Content = "Bardzo dobra margherita, chociaz moglaby byc bardziej chrupiaca.",
            ModerationStatus = ContentModerationStatus.Approved,
            IsVisible = true,
            IsApproved = true,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)),
            CreatedAt = DateTime.UtcNow,
        };
        var review3 = new Review
        {
            ReviewId = 3,
            PublicId = Guid.NewGuid(),
            UserId = 1,
            DishId = 3,
            RestaurantId = 2,
            DishRating = 8,
            ServiceRating = 7,
            CleanlinessRating = 7,
            AmbianceRating = 7,
            Content = "Kebab byl bardzo smaczny i duzy. Mieso aromatyczne.",
            ModerationStatus = ContentModerationStatus.Approved,
            IsVisible = true,
            IsApproved = true,
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            CreatedAt = DateTime.UtcNow,
        };
        var pendingReview = new Review
        {
            ReviewId = 4,
            PublicId = Guid.NewGuid(),
            UserId = 2, // anna-nowak
            DishId = 4, // Tiramisu
            RestaurantId = 1, // Pizzeria Roma
            DishRating = 9,
            ServiceRating = 8,
            CleanlinessRating = 9,
            AmbianceRating = 8,
            Content = "Pyszne tiramisu, najlepsze w miescie!",
            VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            ModerationStatus = ContentModerationStatus.Pending,
            IsVisible = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.Reviews.AddRange(review1, review2, review3, pendingReview);

        // DayOfWeek: 1=Mon, 2=Tue, ..., 6=Sat, 0=Sun
        var weekdays = new[] { 1, 2, 3, 4, 5 }; // Mon-Fri
        var hourId = 1;
        foreach (var day in weekdays)
        {
            db.RestaurantOpeningHours.Add(new RestaurantOpeningHours
            {
                HoursId = hourId++,
                RestaurantId = 1,
                DayOfWeek = day,
                OpenTime = new TimeOnly(11, 0),
                CloseTime = new TimeOnly(22, 0),
                IsClosed = false,
            });
        }

        db.RestaurantOpeningHours.Add(new RestaurantOpeningHours
        {
            HoursId = hourId++,
            RestaurantId = 1,
            DayOfWeek = 6, // Saturday
            OpenTime = new TimeOnly(12, 0),
            CloseTime = new TimeOnly(23, 0),
            IsClosed = false,
        });

        db.RestaurantOpeningHours.Add(new RestaurantOpeningHours
        {
            HoursId = hourId,
            RestaurantId = 1,
            DayOfWeek = 0, // Sunday
            IsClosed = true,
        });

        var pendingPhoto = new MediaAsset
        {
            PublicId = Guid.NewGuid(),
            EntityType = MediaEntityType.Restaurant,
            EntityId = 1, // Pizzeria Roma
            Url = "https://placeholder.test/pizza.jpg",
            UploadedBy = 2, // anna-nowak
            ModerationStatus = ContentModerationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        db.MediaAssets.Add(pendingPhoto);

        var heroImage = new MediaAsset
        {
            AssetId = 100,
            PublicId = Guid.NewGuid(),
            EntityType = MediaEntityType.Hero,
            Url = "/images/restaurant-placeholder.svg",
            CreditText = "Test Hero Image",
            ModerationStatus = ContentModerationStatus.Approved,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };
        db.MediaAssets.Add(heroImage);

        db.ReportReasonDefinitions.AddRange(
            new ReportReasonDefinition { ReasonCode = "spam", LabelPl = "Spam lub reklama", SeverityScore = 1, IsActive = true, CreatedAt = DateTime.UtcNow },
            new ReportReasonDefinition { ReasonCode = "offensive", LabelPl = "Obrazliwa tresc", SeverityScore = 3, IsActive = true, CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();

        db.SystemTickets.AddRange(
            new SystemTicket
            {
                TicketType = TicketType.ReviewContent,
                ReferenceId = pendingReview.ReviewId,
                Status = TicketStatus.Open,
                Priority = 2,
                CreatedAt = DateTime.UtcNow,
            },
            new SystemTicket
            {
                TicketType = TicketType.Photo,
                ReferenceId = pendingPhoto.AssetId,
                Status = TicketStatus.Open,
                Priority = 2,
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        db.SystemConfigs.AddRange(
            new SystemConfig { Key = "moderation.text_batch_size", Value = "100", Description = "Rozmiar paczki moderacji tekstu", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "moderation.image_batch_size", Value = "10", Description = "Rozmiar paczki moderacji obrazow", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "moderation.auto_interval_min", Value = "5", Description = "Interwal automatycznej agregacji moderacji (minuty)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "moderation.auto_enabled", Value = "true", Description = "Włącz/wylacz automatyczna agregacje moderacji", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "auth.refresh_ttl_days", Value = "7", Description = "Czas zycia refresh tokena w dniach", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "auth.refresh_ttl_days_remember", Value = "30", Description = "Czas zycia refresh tokena (Zapamietaj mnie) w dniach", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "auth.access_ttl_sec", Value = "900", Description = "Czas zycia access tokena w sekundach", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "auth.verify_code_ttl_min", Value = "15", Description = "TTL kodow OTP w minutach", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "auth.verify_code_max_attempts", Value = "3", Description = "Max prob kodu weryfikacyjnego", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "auth.max_login_attempts", Value = "5", Description = "Max nieudanych prob logowania przed blokada", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "auth.lockout_duration_min", Value = "15", Description = "Czas blokady konta po przekroczeniu limitu prob (minuty)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "auth.password_min_length", Value = "8", Description = "Min dlugość hasla", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "auth.password_max_length", Value = "128", Description = "Max dlugość hasla", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "auth.password_require_digit", Value = "true", Description = "Haslo wymaga cyfry", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "auth.password_require_special", Value = "true", Description = "Haslo wymaga znaku specjalnego", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "auth.username_min_length", Value = "3", Description = "Min dlugość nazwy uzytkownika", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "auth.username_max_length", Value = "30", Description = "Max dlugość nazwy uzytkownika", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "upload.max_size_mb", Value = "5", Description = "Max rozmiar pliku w MB", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "upload.allowed_types", Value = ".jpg,.jpeg,.png,.webp", Description = "Dozwolone rozszerzenia plikow", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "upload.max_photos_per_review", Value = "5", Description = "Max zdjec na recenzje", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "review.min_length", Value = "10", Description = "Min dlugość recenzji", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "review.max_length", Value = "2000", Description = "Max dlugość recenzji", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "retention.notifications_social_days", Value = "30", Description = "Dni retencji powiadomien spolecznych", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "retention.notifications_system_days", Value = "365", Description = "Dni retencji powiadomien systemowych", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "retention.system_jobs_days", Value = "30", Description = "Dni retencji zakonczonych jobow", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "retention.system_logs_days", Value = "90", Description = "Dni retencji logow systemowych", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "retention.reviews_days", Value = "180", Description = "Dni retencji usunietych recenzji", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "retention.user_deletion_grace_days", Value = "30", Description = "Dni do fizycznego usuniecia uzytkownika", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "search.default_page_size", Value = "20", Description = "Domyslny rozmiar strony API", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "search.max_page_size", Value = "100", Description = "Max rozmiar strony API", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "trending.window_days", Value = "7", Description = "Okno czasowe trendow (dni)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "datacorrection.response_deadline_days", Value = "7", Description = "SLA dni na reakcje na korekte danych", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "ratelimit.auth.permit_limit", Value = "10", Description = "Rate limit auth endpointow (req/okno)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "ratelimit.auth.window_seconds", Value = "60", Description = "Okno rate limitu auth (sekundy)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "ratelimit.upload.permit_limit", Value = "10", Description = "Rate limit uploadu (req/okno)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "ratelimit.upload.window_seconds", Value = "60", Description = "Okno rate limitu uploadu (sekundy)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "ratelimit.search.permit_limit", Value = "30", Description = "Rate limit wyszukiwania (req/okno)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "ratelimit.search.window_seconds", Value = "60", Description = "Okno rate limitu wyszukiwania (sekundy)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "ratelimit.general.permit_limit", Value = "60", Description = "Rate limit ogolny (req/okno)", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "ratelimit.general.window_seconds", Value = "60", Description = "Okno rate limitu ogolnego (sekundy)", IsSecret = false, IsPublic = false });

        db.SystemLogs.AddRange(
            new SystemLog { Source = "AuthService", Level = Domain.Enums.LogLevel.Info, Message = "User jan-kowalski logged in", CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new SystemLog { Source = "ModerationService", Level = Domain.Enums.LogLevel.Warning, Message = "Review flagged for manual review", CreatedAt = DateTime.UtcNow.AddMinutes(-30) },
            new SystemLog { Source = "StorageService", Level = Domain.Enums.LogLevel.Error, Message = "Failed to upload file: connection timeout", Context = "{\"AssetId\": 99}", CreatedAt = DateTime.UtcNow.AddMinutes(-10) });

        db.SystemJobs.AddRange(
            new SystemJob { Type = "text_moderation", Status = JobStatus.Completed, Priority = 1, Progress = 100, CreatedAt = DateTime.UtcNow.AddHours(-2), FinishedAt = DateTime.UtcNow.AddHours(-1) },
            new SystemJob { Type = "image_moderation", Status = JobStatus.Pending, Priority = 2, Progress = 0, CreatedAt = DateTime.UtcNow.AddMinutes(-5) });

        db.RestaurantEditRequests.Add(new RestaurantEditRequest
        {
            RestaurantId = 1, // Pizzeria Roma
            UserId = 1, // jan-kowalski
            Status = EditRequestStatus.Pending,
            ChangeType = EditRequestChangeType.InfoUpdate,
            ChangeScope = EditRequestChangeScope.Restaurant,
            Payload = "{\"phone\": \"+48 111 222 333\"}",
            NewPhone = "+48 111 222 333",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        });

        db.UserSessions.Add(new UserSession
        {
            UserId = 1, // jan-kowalski
            RefreshTokenHash = "stub-session-hash-for-e2e",
            DeviceName = "E2E Browser",
            IpAddress = "127.0.0.1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            LastActiveAt = DateTime.UtcNow,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
        });

        db.AuditLogs.AddRange(
            new AuditLog { TableName = "config", RecordId = 1, Operation = AuditOperation.Insert, ChangedBy = "admin (4)", ChangedAt = DateTime.UtcNow, NewValues = "{\"key\":\"test\",\"value\":\"1\"}" },
            new AuditLog { TableName = "cities", RecordId = 1, Operation = AuditOperation.Update, ChangedBy = "admin (4)", ChangedAt = DateTime.UtcNow.AddHours(-1), OldValues = "{\"name\":\"Wawa\"}", NewValues = "{\"name\":\"Warszawa\"}" },
            new AuditLog { TableName = "ingredients", RecordId = 1, Operation = AuditOperation.Delete, ChangedBy = "admin (4)", ChangedAt = DateTime.UtcNow.AddHours(-2), OldValues = "{\"name\":\"test\"}" });

        db.SecurityLogs.AddRange(
            new SecurityLog { EventType = SecurityEventType.FailedLogin, Email = "hacker@test.com", IpAddress = "192.168.1.100", UserAgent = "Mozilla/5.0", CreatedAt = DateTime.UtcNow },
            new SecurityLog { EventType = SecurityEventType.PasswordChanged, Email = "jan.kowalski@gmail.com", UserId = 1, IpAddress = "10.0.0.50", CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new SecurityLog { EventType = SecurityEventType.BannedRegistration, Email = "zbanowany@smakosz.test", IpAddress = "10.0.0.1", CreatedAt = DateTime.UtcNow.AddHours(-2) });

        db.SystemNodes.AddRange(
            new SystemNode { NodeId = "api-main", NodeType = NodeType.Api, Role = NodeRole.Dispatcher, Status = "online", Hostname = "vps-hetzner", IpAddress = "10.0.0.1", LastHeartbeat = DateTime.UtcNow },
            new SystemNode { NodeId = "gpu-worker-1", NodeType = NodeType.Gpu, Role = NodeRole.Worker, Status = "offline", GpuName = "RTX 3060", GpuMemoryTotal = 12288, GpuMemoryUsed = 0, LastHeartbeat = DateTime.UtcNow.AddDays(-1) },
            new SystemNode { NodeId = "orchestrator", NodeType = NodeType.Orchestrator, Role = NodeRole.Dispatcher, Status = "online", Hostname = "vps-hetzner", IpAddress = "10.0.0.2", LastHeartbeat = DateTime.UtcNow },
            new SystemNode { NodeId = "rpi-gateway", NodeType = NodeType.RpiGateway, Status = "online", Hostname = "raspberrypi", IpAddress = "10.0.0.3", LastHeartbeat = DateTime.UtcNow });

        // Second save: link marco to his restaurant (breaks circular dependency)
        marco.RestaurantId = 1;
        await db.SaveChangesAsync();

        // Without this, the next auto-generated ID would conflict with seeded rows
        await db.Database.ExecuteSqlRawAsync(@"
            SELECT setval(pg_get_serial_sequence('users', 'user_id'), (SELECT MAX(user_id) FROM users));
            SELECT setval(pg_get_serial_sequence('restaurants', 'restaurant_id'), (SELECT MAX(restaurant_id) FROM restaurants));
            SELECT setval(pg_get_serial_sequence('dishes', 'dish_id'), (SELECT MAX(dish_id) FROM dishes));
            SELECT setval(pg_get_serial_sequence('reviews', 'review_id'), (SELECT MAX(review_id) FROM reviews));
            SELECT setval(pg_get_serial_sequence('cities', 'city_id'), (SELECT MAX(city_id) FROM cities));
            SELECT setval(pg_get_serial_sequence('restaurant_opening_hours', 'hours_id'), (SELECT MAX(hours_id) FROM restaurant_opening_hours));
            SELECT setval(pg_get_serial_sequence('cuisine_types', 'cuisine_type_id'), (SELECT MAX(cuisine_type_id) FROM cuisine_types));
            SELECT setval(pg_get_serial_sequence('media_assets', 'asset_id'), (SELECT COALESCE(MAX(asset_id), 1) FROM media_assets));
            SELECT setval(pg_get_serial_sequence('system.tickets', 'ticket_id'), (SELECT COALESCE(MAX(ticket_id), 1) FROM system.tickets));
            SELECT setval(pg_get_serial_sequence('system.logs', 'id'), (SELECT COALESCE(MAX(id), 1) FROM system.logs));
            SELECT setval(pg_get_serial_sequence('system.jobs', 'job_id'), (SELECT COALESCE(MAX(job_id), 1) FROM system.jobs));
            SELECT setval(pg_get_serial_sequence('restaurant_edit_requests', 'request_id'), (SELECT COALESCE(MAX(request_id), 1) FROM restaurant_edit_requests));
            SELECT setval(pg_get_serial_sequence('user_sessions', 'user_session_id'), (SELECT COALESCE(MAX(user_session_id), 1) FROM user_sessions));
            SELECT setval(pg_get_serial_sequence('system.forbidden_words', 'word_id'), (SELECT COALESCE(MAX(word_id), 1) FROM system.forbidden_words));
            SELECT setval(pg_get_serial_sequence('menu_sections', 'section_id'), (SELECT COALESCE(MAX(section_id), 1) FROM menu_sections));
            SELECT setval(pg_get_serial_sequence('tags', 'tag_id'), (SELECT COALESCE(MAX(tag_id), 1) FROM tags));
            SELECT setval(pg_get_serial_sequence('ingredients', 'ingredient_id'), (SELECT COALESCE(MAX(ingredient_id), 1) FROM ingredients));
            SELECT setval(pg_get_serial_sequence('audit_logs', 'audit_log_id'), (SELECT COALESCE(MAX(audit_log_id), 1) FROM audit_logs));
            SELECT setval(pg_get_serial_sequence('system.security_logs', 'log_id'), (SELECT COALESCE(MAX(log_id), 1) FROM system.security_logs));
        ");
    }
}
