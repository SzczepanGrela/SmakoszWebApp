// Controllers/HomeController.cs
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.Models;
using SmakoszWebApp.ViewModels;
using System.Diagnostics;

namespace SmakoszWebApp.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var viewModel = new HomePageViewModel
            {
                PopularCategories = GetPopularCategories(),
                HighlyRatedDishes = GetMockDishes()
            };
            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // --- Metody do generowania danych tymczasowych ---

        private List<string> GetPopularCategories()
        {
            return new List<string> { "Pizza", "Burgery", "Sushi", "Ramen", "Pierogi", "Desery" };
        }

        private List<DishViewModel> GetMockDishes()
        {
            return new List<DishViewModel>
            {
                new DishViewModel
                {
                    Id = 1,
                    Name = "Pizza Diavola",
                    RestaurantName = "Pizzeria Roma",
                    RestaurantId = 1,
                    Price = 38.00m,
                    AverageRating = 4.8,
                    ReviewCount = 45,
                    ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza",
                    Tags = new List<string> { "Ostre" }
                },
                new DishViewModel
                {
                    Id = 2,
                    Name = "Klasyczny Burger Wołowy",
                    RestaurantName = "Burgerownia Stacja",
                    RestaurantId = 2,
                    Price = 42.00m,
                    AverageRating = 4.9,
                    ReviewCount = 120,
                    ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Burger",
                    Tags = new List<string>()
                },
                new DishViewModel
                {
                    Id = 3,
                    Name = "Zestaw Sushi Ebi Ten",
                    RestaurantName = "Sushi Master",
                    RestaurantId = 3,
                    Price = 55.00m,
                    AverageRating = 4.7,
                    ReviewCount = 78,
                    ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi",
                    Tags = new List<string> { "Owoce morza" }
                },
                new DishViewModel
                {
                    Id = 4,
                    Name = "Tantanmen Ramen",
                    RestaurantName = "Ramen-Ya",
                    RestaurantId = 4,
                    Price = 45.00m,
                    AverageRating = 4.9,
                    ReviewCount = 95,
                    ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Ramen",
                    Tags = new List<string> { "Ostre", "Wegańskie" }
                }
            };
        }
    }
}