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
            new CuisineType { CuisineTypeId = 1, Name = "Wloska", DisplayName = "Wloska" },
            new CuisineType { CuisineTypeId = 2, Name = "Turecka", DisplayName = "Turecka" },
            new CuisineType { CuisineTypeId = 3, Name = "Polska", DisplayName = "Polska" });

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
            CuisineType = "Wloska",
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
            CuisineType = "Turecka",
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
            CuisineType = "Polska",
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
            IsSpicy = true,
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
        db.Dishes.AddRange(margherita, pepperoni, kebabDuzy, tiramisu);

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
            ContentStatus = ReviewContentStatus.Approved,
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
            ContentStatus = ReviewContentStatus.Approved,
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
            ContentStatus = ReviewContentStatus.Approved,
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
            ContentStatus = ReviewContentStatus.Pending,
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
            Status = MediaAssetStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };
        db.MediaAssets.Add(pendingPhoto);

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
            new SystemConfig { Key = "moderation.auto_approve_threshold", Value = "0.85", Description = "Prog auto-zatwierdzania tresci", IsSecret = false, IsPublic = false },
            new SystemConfig { Key = "moderation.max_reports_before_hide", Value = "3", Description = "Maks. zgloszenia przed ukryciem", IsSecret = false, IsPublic = true },
            new SystemConfig { Key = "api.rate_limit_per_minute", Value = "60", Description = "Limit zapytan API/min", IsSecret = false, IsPublic = false });

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
        ");
    }
}
