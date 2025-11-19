// Controllers/ReviewController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;

namespace SmakoszWebApp.Controllers
{
    public class ReviewController : Controller
    {
        public IActionResult Add(int? dishId)
        {
            // ✅ FIX: Validate dishId
            if (dishId.HasValue && dishId.Value <= 0)
            {
                return BadRequest("Invalid dish ID.");
            }

            ViewBag.DishId = dishId;
            return View();
        }

        [HttpPost]
        public IActionResult Add(AddReviewViewModel model)
        {
            // ✅ FIX: Validate model
            if (model == null)
            {
                return BadRequest("Review data is required.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

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
                new { Id = 2, Name = "Burger Classic", RestaurantName = "Burgerownia Stacja", ImageUrl = "https://placehold.co/60x60/4CAF50/white?text=B" },
                new { Id = 3, Name = "Sushi Roll", RestaurantName = "Sushi Master", ImageUrl = "https://placehold.co/60x60/2196F3/white?text=S" }
            };

            return Json(results);
        }

        [HttpPost]
        public IActionResult Report([FromBody] ReportReviewRequest request)
        {
            // ✅ FIX: Validate request and ReviewId
            if (request == null)
            {
                return BadRequest("Request is required.");
            }

            if (request.ReviewId <= 0)
            {
                return BadRequest("Invalid review ID.");
            }

            // Mock - w rzeczywistości zapisałoby zgłoszenie do bazy
            return Json(new { success = true, message = "Recenzja została zgłoszona." });
        }
    }

    public class ReportReviewRequest
    {
        public int ReviewId { get; set; }
    }
}