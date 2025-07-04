// Controllers/RestaurantController.cs
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;

namespace SmakoszWebApp.Controllers
{
    public class RestaurantController : Controller
    {
        // GET: /Restaurant/Index/1
        public IActionResult Index(int id)
        {
            var restaurant = GetMockRestaurants().FirstOrDefault(r => r.Id == id);
            if (restaurant == null)
            {
                return NotFound();
            }

            return View(restaurant);
        }

        private List<RestaurantViewModel> GetMockRestaurants()
        {
            return new List<RestaurantViewModel>
            {
                new RestaurantViewModel
                {
                    Id = 1,
                    Name = "Pizzeria Roma",
                    City = "Rzeszów",
                    Address = "ul. Grunwaldzka 1",
                    AverageRating = 4.6,
                    Description = "Tradycyjna włoska pizzeria z piecem opalanym drewnem. Serwujemy klasyki kuchni włoskiej od 2005 roku.",
                    ImageUrl = "https://placehold.co/1200x400/ff6f61/white?text=Pizzeria+Roma",
                    Dishes = new List<DishViewModel>
                    {
                         new DishViewModel { Id = 1, Name = "Pizza Diavola", RestaurantName = "Pizzeria Roma", Price = 38.00m, AverageRating = 4.8, ReviewCount = 45, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza" }
                    }
                },
                new RestaurantViewModel
                {
                    Id = 2,
                    Name = "Burgerownia Stacja",
                    City = "Rzeszów",
                    Address = "ul. 3 Maja 15",
                    AverageRating = 4.9,
                    Description = "Najlepsze burgery w mieście. Używamy tylko świeżej, lokalnej wołowiny i autorskich sosów.",
                    ImageUrl = "https://placehold.co/1200x400/4CAF50/white?text=Burgerownia",
                    Dishes = new List<DishViewModel>
                    {
                        new DishViewModel { Id = 2, Name = "Klasyczny Burger Wołowy", RestaurantName = "Burgerownia Stacja", Price = 42.00m, AverageRating = 4.9, ReviewCount = 120, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Burger" }
                    }
                }
            };
        }
    }
}