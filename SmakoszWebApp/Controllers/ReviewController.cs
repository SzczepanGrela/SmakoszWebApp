// Controllers/ReviewController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;

namespace SmakoszWebApp.Controllers
{
    public class ReviewController : Controller
    {
        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Add(AddReviewViewModel model)
        {
            // Mock - w rzeczywistości zapisałoby do bazy
            return Json(new { success = true, message = "Ocena została dodana!" });
        }

        [HttpGet]
        public IActionResult SearchDishes(string query)
        {
            // Mock search results
            var results = new List<dynamic>
            {
                new { Id = 1, Name = "Pizza Margherita", RestaurantName = "Pizzeria Roma", ImageUrl = "https://placehold.co/60x60/ff6f61/white?text=P" },
                new { Id = 2, Name = "Burger Classic", RestaurantName = "Burgerownia", ImageUrl = "https://placehold.co/60x60/4CAF50/white?text=B" },
                new { Id = 3, Name = "Sushi Roll", RestaurantName = "Sushi Master", ImageUrl = "https://placehold.co/60x60/2196F3/white?text=S" }
            };

            return Json(results);
        }
    }
}