// Controllers/HomeController.cs
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.Models;
using SmakoszWebApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmakoszWebApp.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var isLoggedIn = User.Identity.IsAuthenticated;
            
            var viewModel = new HomePageViewModel
            {
                PopularCategories = GetPopularCategories(),
                HighlyRatedDishes = GetMockDishes(),
                RecommendedDishes = GetRecommendedDishes(),
                NewDiscoveries = GetNewDiscoveries(),
                QuickChoices = GetQuickChoices(),
                RecentReviews = GetRecentReviews(),
                Stats = GetHomePageStats(),
                IsUserLoggedIn = isLoggedIn,
                AllDishesForRandom = GetAllDishesForRandom()
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
            return new List<string> { "Pizza", "Burgery", "Sushi", "Ramen", "Pierogi", "Desery", "Pasta", "Kebab", "Tacos", "Curry", "Salads", "Seafood" };
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

        private List<DishViewModel> GetRecommendedDishes()
        {
            return new List<DishViewModel>
            {
                new DishViewModel
                {
                    Id = 5,
                    Name = "Ramen Tonkotsu Special",
                    RestaurantName = "Ramen-Ya",
                    RestaurantId = 4,
                    Price = 52.00m,
                    AverageRating = 4.9,
                    ReviewCount = 156,
                    ImageUrl = "https://placehold.co/400x300/FFC107/white?text=Ramen+Special",
                    Tags = new List<string> { "Polecane", "Nowe" }
                },
                new DishViewModel
                {
                    Id = 6,
                    Name = "Neapolitan Pizza Margherita",
                    RestaurantName = "Pizzeria Italiana",
                    RestaurantId = 5,
                    Price = 36.00m,
                    AverageRating = 4.8,
                    ReviewCount = 203,
                    ImageUrl = "https://placehold.co/400x300/ff6f61/white?text=Margherita",
                    Tags = new List<string> { "Klasyka", "Wegetariańskie" }
                },
                new DishViewModel
                {
                    Id = 7,
                    Name = "Pad Thai Chicken",
                    RestaurantName = "Thai Smile",
                    RestaurantId = 6,
                    Price = 41.00m,
                    AverageRating = 4.7,
                    ReviewCount = 89,
                    ImageUrl = "https://placehold.co/400x300/FF9800/white?text=Pad+Thai",
                    Tags = new List<string> { "Ostre", "Azjatyckie" }
                }
            };
        }


        private List<DishViewModel> GetNewDiscoveries()
        {
            return new List<DishViewModel>
            {
                new DishViewModel
                {
                    Id = 8,
                    Name = "Korean BBQ Bibimbap",
                    RestaurantName = "Seoul Kitchen",
                    RestaurantId = 7,
                    Price = 47.00m,
                    AverageRating = 4.8,
                    ReviewCount = 34,
                    ImageUrl = "https://placehold.co/300x200/E91E63/white?text=Korean",
                    Tags = new List<string> { "Nowe", "Koreańskie" }
                },
                new DishViewModel
                {
                    Id = 9,
                    Name = "Truffle Pasta Carbonara",
                    RestaurantName = "Pasta Perfetta",
                    RestaurantId = 8,
                    Price = 67.00m,
                    AverageRating = 4.9,
                    ReviewCount = 23,
                    ImageUrl = "https://placehold.co/300x200/8BC34A/white?text=Truffle",
                    Tags = new List<string> { "Nowe", "Luksusowe" }
                }
            };
        }


        private List<string> GetQuickChoices()
        {
            return new List<string> { "Szybko i tanio", "Dla smakoszy", "Wegetariańskie", "Ostre dania", "Comfort food", "Zdrowe opcje" };
        }

        private List<ReviewViewModel> GetRecentReviews()
        {
            return new List<ReviewViewModel>
            {
                new ReviewViewModel
                {
                    Id = 1,
                    DishName = "Pizza Diavola",
                    RestaurantName = "Pizzeria Roma",
                    Rating = 5,
                    Comment = "Absolutnie fantastyczna pizza! Idealna ostrość i świeże składniki.",
                    UserName = "Anna K.",
                    Date = DateTime.Now.AddHours(-2),
                    DishId = 1
                },
                new ReviewViewModel
                {
                    Id = 2,
                    DishName = "Ramen Shoyu",
                    RestaurantName = "Ramen-Ya",
                    Rating = 5,
                    Comment = "Najlepszy ramen w mieście! Bulion idealny, makaron al dente.",
                    UserName = "Tomasz M.",
                    Date = DateTime.Now.AddHours(-5),
                    DishId = 4
                },
                new ReviewViewModel
                {
                    Id = 3,
                    DishName = "Burger Wołowy",
                    RestaurantName = "Burgerownia",
                    Rating = 4,
                    Comment = "Solidny burger, mięso smakowite, ale bułka mogłaby być lepsza.",
                    UserName = "Marcin L.",
                    Date = DateTime.Now.AddHours(-8),
                    DishId = 2
                }
            };
        }

        private HomePageStats GetHomePageStats()
        {
            return new HomePageStats
            {
                TotalDishes = 2847,
                TotalRestaurants = 156,
                TotalReviews = 8923,
                ActiveUsers = 1234
            };
        }

        private List<object> GetAllDishesForRandom()
        {
            var allDishes = GetMockDishes()
                .Concat(GetNewDiscoveries())
                .Concat(GetRecommendedDishes())
                .Select(d => new { d.Id, d.Name })
                .ToList<object>();
            return allDishes;
        }
    }
}