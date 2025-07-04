// Controllers/ProfileController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;
using System.Security.Claims;

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
                MyRatedDishes = GetMockDishes(3), // Pobierz 3 przykładowe dania ocenione
                SavedForLaterDishes = GetMockDishes(2, 3) // Pobierz 2 inne dania zapisane
            };
            return View(viewModel);
        }

        // Metoda pomocnicza do generowania danych tymczasowych
        private List<DishViewModel> GetMockDishes(int count, int skip = 0)
        {
            var allDishes = new List<DishViewModel>
            {
                new DishViewModel { Id = 1, Name = "Pizza Diavola", RestaurantName = "Pizzeria Roma", Price = 38.00m, AverageRating = 4.8, ReviewCount = 45, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza" },
                new DishViewModel { Id = 2, Name = "Klasyczny Burger Wołowy", RestaurantName = "Burgerownia Stacja", Price = 42.00m, AverageRating = 4.9, ReviewCount = 120, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Burger" },
                new DishViewModel { Id = 3, Name = "Zestaw Sushi Ebi Ten", RestaurantName = "Sushi Master", Price = 55.00m, AverageRating = 4.7, ReviewCount = 78, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi" },
                new DishViewModel { Id = 4, Name = "Tantanmen Ramen", RestaurantName = "Ramen-Ya", Price = 45.00m, AverageRating = 4.9, ReviewCount = 95, ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Ramen" },
                 new DishViewModel { Id = 5, Name = "Sernik Baskijski", RestaurantName = "Słodka Dziurka", Price = 25.00m, AverageRating = 5.0, ReviewCount = 210, ImageUrl = "https://placehold.co/600x400/9C27B0/white?text=Sernik" }
            };
            return allDishes.Skip(skip).Take(count).ToList();
        }
    }
}