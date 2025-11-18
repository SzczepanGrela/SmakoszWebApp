// Controllers/SearchController.cs
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;

namespace SmakoszWebApp.Controllers
{
    public class SearchController : Controller
    {
        // GET: /Search?query=...
        public IActionResult Index(string query)
        {
            var results = GetMockDishes(); // Na razie zwracamy wszystkie dania

            var viewModel = new SearchResultsViewModel
            {
                Query = query,
                Results = results
            };

            return View(viewModel);
        }

        private List<DishViewModel> GetMockDishes()
        {
            // Ta sama metoda, co w HomeController dla spójności danych
            return new List<DishViewModel>
            {
                new DishViewModel
                {
                    Id = 1,
                    Name = "Pizza Diavola",
                    RestaurantName = "Pizzeria Roma",
                    Price = 38.00m,
                    AverageRating = 9.6,
                    ReviewCount = 45,
                    ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza",
                    Tags = new List<string> { "Ostre" }
                },
                new DishViewModel
                {
                    Id = 2,
                    Name = "Klasyczny Burger Wołowy",
                    RestaurantName = "Burgerownia Stacja",
                    Price = 42.00m,
                    AverageRating = 9.8,
                    ReviewCount = 120,
                    ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Burger",
                    Tags = new List<string>()
                },
                new DishViewModel
                {
                    Id = 3,
                    Name = "Zestaw Sushi Ebi Ten",
                    RestaurantName = "Sushi Master",
                    Price = 55.00m,
                    AverageRating = 9.4,
                    ReviewCount = 78,
                    ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi",
                    Tags = new List<string> { "Owoce morza" }
                }
            };
        }
    }
}