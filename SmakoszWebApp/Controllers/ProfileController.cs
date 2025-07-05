// Controllers/ProfileController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;
using System.Security.Claims;
using System.Linq;
using System;

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
                UserName = User.Identity.Name,
                UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U",
                TotalReviews = 15,
                TotalPhotos = 5,
                MyRatedDishes = GetMockReviews(3), // Pobierz 3 przykładowe recenzje
                SavedForLaterDishes = GetMockReviews(2, 3) // Pobierz 2 inne recenzje
            };
            return View(viewModel);
        }

        // Metoda pomocnicza do generowania danych tymczasowych
        private List<ReviewViewModel> GetMockReviews(int count, int skip = 0)
        {
            var allReviews = new List<ReviewViewModel>
            {
                new ReviewViewModel { Id = 1, UserName = User.Identity.Name, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 4.8, Comment = "Fantastyczna pizza z ostrymi pepperoni! Ciasto idealne, składniki świeże.", Date = DateTime.Now.AddDays(-2), DatePosted = DateTime.Now.AddDays(-2), DishName = "Pizza Diavola", RestaurantName = "Pizzeria Roma", DishId = 1 },
                new ReviewViewModel { Id = 2, UserName = User.Identity.Name, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 4.9, Comment = "Najlepszy burger w mieście! Mięso soczyste, dodatki świeże, obsługa super.", Date = DateTime.Now.AddDays(-5), DatePosted = DateTime.Now.AddDays(-5), DishName = "Klasyczny Burger Wołowy", RestaurantName = "Burgerownia Stacja", DishId = 2 },
                new ReviewViewModel { Id = 3, UserName = User.Identity.Name, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 4.7, Comment = "Sushi bardzo smaczne, ryba świeża. Tempura krewetki doskonała.", Date = DateTime.Now.AddDays(-1), DatePosted = DateTime.Now.AddDays(-1), DishName = "Zestaw Sushi Ebi Ten", RestaurantName = "Sushi Master", DishId = 3 },
                new ReviewViewModel { Id = 4, UserName = User.Identity.Name, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 4.9, Comment = "Autentyczny ramen o intensywnym smaku. Makaron al dente, bulion bogaty.", Date = DateTime.Now.AddDays(-3), DatePosted = DateTime.Now.AddDays(-3), DishName = "Tantanmen Ramen", RestaurantName = "Ramen-Ya", DishId = 4 },
                new ReviewViewModel { Id = 5, UserName = User.Identity.Name, UserAvatarUrl = "https://placehold.co/150x150/ff6f61/white?text=U", Rating = 5.0, Comment = "Najlepszy sernik baskijski jaki jadłem! Idealnie karmelizowany, kremowy w środku.", Date = DateTime.Now.AddDays(-4), DatePosted = DateTime.Now.AddDays(-4), DishName = "Sernik Baskijski", RestaurantName = "Słodka Dziurka", DishId = 5 }
            };
            return allReviews.Skip(skip).Take(count).ToList();
        }
    }
}