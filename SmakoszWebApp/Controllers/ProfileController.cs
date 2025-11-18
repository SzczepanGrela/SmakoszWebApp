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
            var viewModel = new ProfileViewModel
            {
                // Pobieramy email zalogowanego użytkownika
                UserName = User.Identity?.Name,
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
            var userName = User.Identity?.Name;
            var allReviews = new List<ReviewViewModel>
            {
                new ReviewViewModel { Id = 1, UserName = userName, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 9.6, Comment = "Fantastyczna pizza z ostrymi pepperoni! Ciasto idealne, składniki świeże.", Date = DateTime.Now.AddDays(-2), DatePosted = DateTime.Now.AddDays(-2), DishName = "Pizza Diavola", RestaurantName = "Pizzeria Roma", DishId = 1 },
                new ReviewViewModel { Id = 2, UserName = userName, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 9.8, Comment = "Najlepszy burger w mieście! Mięso soczyste, dodatki świeże, obsługa super.", Date = DateTime.Now.AddDays(-5), DatePosted = DateTime.Now.AddDays(-5), DishName = "Klasyczny Burger Wołowy", RestaurantName = "Burgerownia Stacja", DishId = 2 },
                new ReviewViewModel { Id = 3, UserName = userName, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 9.4, Comment = "Sushi bardzo smaczne, ryba świeża. Tempura krewetki doskonała.", Date = DateTime.Now.AddDays(-1), DatePosted = DateTime.Now.AddDays(-1), DishName = "Zestaw Sushi Ebi Ten", RestaurantName = "Sushi Master", DishId = 3 },
                new ReviewViewModel { Id = 4, UserName = userName, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 9.8, Comment = "Autentyczny ramen o intensywnym smaku. Makaron al dente, bulion bogaty.", Date = DateTime.Now.AddDays(-3), DatePosted = DateTime.Now.AddDays(-3), DishName = "Tantanmen Ramen", RestaurantName = "Ramen-Ya", DishId = 4 },
                new ReviewViewModel { Id = 5, UserName = userName, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 10.0, Comment = "Najlepszy sernik baskijski jaki jadłem! Idealnie karmelizowany, kremowy w środku.", Date = DateTime.Now.AddDays(-4), DatePosted = DateTime.Now.AddDays(-4), DishName = "Sernik Baskijski", RestaurantName = "Słodka Dziurka", DishId = 5 }
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