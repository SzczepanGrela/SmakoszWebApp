// Controllers/ProfileController.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;

namespace SmakoszWebApp.Controllers
{
    [Authorize] // Ten atrybut zabezpiecza cały kontroler
    public class ProfileController : Controller
    {
        public IActionResult Index()
        {
            // ✅ FIX: Safely handle potentially null User.Identity.Name
            var userName = User.Identity?.Name ?? "Anonymous";

            var viewModel = new ProfileViewModel
            {
                // Pobieramy email zalogowanego użytkownika
                UserName = userName,
                UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U",
                TotalReviews = 15,
                TotalPhotos = 5,
                MyRatedDishes = GetMockReviews(3), // Pobierz 3 przykładowe recenzje
                SavedForLaterDishes = GetMockSavedDishes(2) // Pobierz 2 zapisane dania
            };
            return View(viewModel);
        }

        // Metoda pomocnicza do generowania danych tymczasowych
        private List<ReviewViewModel> GetMockReviews(int count, int skip = 0)
        {
            // ✅ FIX: Safely handle potentially null User.Identity.Name
            var userName = User.Identity?.Name ?? "Anonymous";
            var allReviews = new List<ReviewViewModel>
            {
                new ReviewViewModel
                {
                    Id = 1,
                    UserId = 1,
                    RestaurantId = 1,
                    DishId = 1,
                    UserName = userName,
                    UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U",
                    DishRating = 10,
                    ServiceRating = 9,
                    CleanlinessRating = 10,
                    ReviewTitle = "Fantastyczna pizza!",
                    Comment = "Fantastyczna pizza z ostrymi pepperoni! Ciasto idealne, składniki świeże.",
                    ReviewDate = DateTime.Now.AddDays(-2),
                    HelpfulCount = 5,
                    IsApproved = true,
                    DishName = "Pizza Diavola",
                    RestaurantName = "Pizzeria Roma"
                },
                new ReviewViewModel
                {
                    Id = 2,
                    UserId = 1,
                    RestaurantId = 2,
                    DishId = 2,
                    UserName = userName,
                    UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U",
                    DishRating = 10,
                    ServiceRating = 9,
                    AmbianceRating = 10,
                    ReviewTitle = "Najlepszy burger!",
                    Comment = "Najlepszy burger w mieście! Mięso soczyste, dodatki świeże, obsługa super.",
                    ReviewDate = DateTime.Now.AddDays(-5),
                    HelpfulCount = 8,
                    IsApproved = true,
                    DishName = "Klasyczny Burger Wołowy",
                    RestaurantName = "Burgerownia Stacja"
                },
                new ReviewViewModel
                {
                    Id = 3,
                    UserId = 1,
                    RestaurantId = 3,
                    DishId = 3,
                    UserName = userName,
                    UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U",
                    DishRating = 9,
                    ServiceRating = 10,
                    ReviewTitle = "Świeże sushi",
                    Comment = "Sushi bardzo smaczne, ryba świeża. Tempura krewetki doskonała.",
                    ReviewDate = DateTime.Now.AddDays(-1),
                    HelpfulCount = 3,
                    IsApproved = true,
                    DishName = "Zestaw Sushi Ebi Ten",
                    RestaurantName = "Sushi Master"
                },
                new ReviewViewModel
                {
                    Id = 4,
                    UserId = 1,
                    RestaurantId = 4,
                    DishId = 4,
                    UserName = userName,
                    UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U",
                    DishRating = 10,
                    ServiceRating = 9,
                    CleanlinessRating = 9,
                    AmbianceRating = 10,
                    ReviewTitle = "Autentyczny ramen",
                    Comment = "Autentyczny ramen o intensywnym smaku. Makaron al dente, bulion bogaty.",
                    ReviewDate = DateTime.Now.AddDays(-3),
                    HelpfulCount = 12,
                    IsApproved = true,
                    DishName = "Tantanmen Ramen",
                    RestaurantName = "Ramen-Ya"
                },
                new ReviewViewModel
                {
                    Id = 5,
                    UserId = 1,
                    RestaurantId = 9,
                    DishId = 5,
                    UserName = userName,
                    UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U",
                    DishRating = 10,
                    ServiceRating = 8,
                    CleanlinessRating = 9,
                    ReviewTitle = "Najlepszy sernik!",
                    Comment = "Najlepszy sernik baskijski jaki jadłem! Idealnie karmelizowany, kremowy w środku.",
                    ReviewDate = DateTime.Now.AddDays(-4),
                    HelpfulCount = 7,
                    IsApproved = true,
                    DishName = "Sernik Baskijski",
                    RestaurantName = "Słodka Dziurka"
                }
            };
            return allReviews.Skip(skip).Take(count).ToList();
        }

        // Metoda pomocnicza do generowania danych tymczasowych dla zapisanych dań
        private List<DishViewModel> GetMockSavedDishes(int count)
        {
            var savedDishes = new List<DishViewModel>
            {
                new DishViewModel
                {
                    Id = 6,
                    Name = "Neapolitan Pizza Margherita",
                    RestaurantName = "Pizzeria Italiana",
                    RestaurantId = 5,
                    Price = 36.00m,
                    AverageRating = 9.6,
                    ReviewCount = 203,
                    ImageUrl = "https://placehold.co/400x300/ff6f61/white?text=Margherita",
                    Tags = new List<string> { "Klasyka", "Wegetariańskie" },
                    Description = "Klasyczna pizza z San Marzano, mozzarellą i świeżą bazylią"
                },
                new DishViewModel
                {
                    Id = 7,
                    Name = "Pad Thai Chicken",
                    RestaurantName = "Thai Garden",
                    RestaurantId = 6,
                    Price = 41.00m,
                    AverageRating = 9.4,
                    ReviewCount = 89,
                    ImageUrl = "https://placehold.co/400x300/FF9800/white?text=Pad+Thai",
                    Tags = new List<string> { "Ostre", "Azjatyckie" },
                    Description = "Tradycyjny makaron ryżowy z kurczakiem, kiełkami i sosem tamaryndowym"
                },
                new DishViewModel
                {
                    Id = 8,
                    Name = "Korean BBQ Bibimbap",
                    RestaurantName = "Seoul Kitchen",
                    RestaurantId = 7,
                    Price = 47.00m,
                    AverageRating = 9.6,
                    ReviewCount = 34,
                    ImageUrl = "https://placehold.co/400x300/E91E63/white?text=Korean",
                    Tags = new List<string> { "Nowe", "Koreańskie" },
                    Description = "Koreańska miska z ryżem, warzywami i marynowanym mięsem"
                }
            };
            return savedDishes.Take(count).ToList();
        }
    }
}