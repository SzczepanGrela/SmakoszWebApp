// Controllers/RecommendationsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;

namespace SmakoszWebApp.Controllers
{
    [Authorize]
    public class RecommendationsController : Controller
    {
        public IActionResult Index()
        {
            var viewModel = new RecommendationsViewModel
            {
                PersonalizedRecommendations = GetPersonalizedRecommendations(),
                TrendingDishes = GetTrendingDishes(),
                SimilarUsersRecommendations = GetSimilarUsersRecommendations()
            };
            return View(viewModel);
        }

        private List<DishViewModel> GetPersonalizedRecommendations()
        {
            return new List<DishViewModel>
            {
                new DishViewModel { Id = 10, Name = "Ramen Shoyu Deluxe", RestaurantName = "Noodle House Tokyo", RestaurantId = 5, Price = 48.00m, AverageRating = 9.8, ReviewCount = 87, ImageUrl = "https://placehold.co/300x200/ff6f61/white?text=Ramen" },
                new DishViewModel { Id = 11, Name = "Sushi Dragon Roll", RestaurantName = "Sushi Master", RestaurantId = 3, Price = 65.00m, AverageRating = 9.6, ReviewCount = 156, ImageUrl = "https://placehold.co/300x200/2196F3/white?text=Sushi" },
                new DishViewModel { Id = 12, Name = "Pad Thai Premium", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 42.00m, AverageRating = 9.4, ReviewCount = 203, ImageUrl = "https://placehold.co/300x200/FFC107/white?text=Thai" },
                new DishViewModel { Id = 13, Name = "Carbonara Truffle", RestaurantName = "Italiano", RestaurantId = 7, Price = 52.00m, AverageRating = 9.8, ReviewCount = 98, ImageUrl = "https://placehold.co/300x200/4CAF50/white?text=Pasta" }
            };
        }

        private List<DishViewModel> GetTrendingDishes()
        {
            return new List<DishViewModel>
            {
                new DishViewModel { Id = 14, Name = "Burger BBQ Supreme", RestaurantName = "Grill Masters", RestaurantId = 8, Price = 45.00m, AverageRating = 9.6, ReviewCount = 234, ImageUrl = "https://placehold.co/300x200/4CAF50/white?text=BBQ" },
                new DishViewModel { Id = 15, Name = "Pizza Quattro Formaggi", RestaurantName = "Pizzeria Roma", RestaurantId = 1, Price = 41.00m, AverageRating = 9.2, ReviewCount = 167, ImageUrl = "https://placehold.co/300x200/ff6f61/white?text=Pizza" },
                new DishViewModel { Id = 16, Name = "Fish & Chips Classic", RestaurantName = "Burgerownia Stacja", RestaurantId = 2, Price = 38.00m, AverageRating = 9.0, ReviewCount = 189, ImageUrl = "https://placehold.co/300x200/2196F3/white?text=Fish" }
            };
        }

        private List<DishViewModel> GetSimilarUsersRecommendations()
        {
            return new List<DishViewModel>
            {
                new DishViewModel { Id = 17, Name = "Sushi Salmon", RestaurantName = "Sushi Master", RestaurantId = 3, Price = 32.00m, AverageRating = 9.4, ReviewCount = 145, ImageUrl = "https://placehold.co/200x150/2196F3/white?text=Sushi" },
                new DishViewModel { Id = 18, Name = "Tacos Al Pastor", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 28.00m, AverageRating = 9.2, ReviewCount = 201, ImageUrl = "https://placehold.co/200x150/FFC107/white?text=Tacos" },
                new DishViewModel { Id = 19, Name = "Pho Bo Vietnam", RestaurantName = "Noodle House Tokyo", RestaurantId = 5, Price = 35.00m, AverageRating = 9.6, ReviewCount = 178, ImageUrl = "https://placehold.co/200x150/4CAF50/white?text=Pho" },
                new DishViewModel { Id = 20, Name = "Biryani Chicken", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 39.00m, AverageRating = 9.4, ReviewCount = 167, ImageUrl = "https://placehold.co/200x150/FF9800/white?text=Biryani" },
                new DishViewModel { Id = 21, Name = "Gyros Greek", RestaurantName = "Italiano", RestaurantId = 7, Price = 26.00m, AverageRating = 9.0, ReviewCount = 198, ImageUrl = "https://placehold.co/200x150/9C27B0/white?text=Gyros" },
                new DishViewModel { Id = 22, Name = "Kebab Turkish", RestaurantName = "Grill Masters", RestaurantId = 8, Price = 24.00m, AverageRating = 8.8, ReviewCount = 234, ImageUrl = "https://placehold.co/200x150/795548/white?text=Kebab" }
            };
        }

        [HttpPost]
        public IActionResult SaveForLater(int dishId)
        {
            // Mock - w rzeczywistości zapisałoby do bazy
            return Json(new { success = true, message = "Danie zostało zapisane na później!" });
        }

        [HttpPost]
        public IActionResult AddToFavorites(int dishId)
        {
            // Mock - w rzeczywistości zapisałoby do bazy
            return Json(new { success = true, message = "Danie zostało dodane do ulubionych!" });
        }
    }
}