// Controllers/DishController.cs
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;

namespace SmakoszWebApp.Controllers
{
    public class DishController : Controller
    {
        // GET: /Dish/Index/1
        public IActionResult Index(int id)
        {
            // Znajdź danie o podanym id w naszej liście mocków
            var dish = GetMockDishes().FirstOrDefault(d => d.Id == id);

            if (dish == null)
            {
                return NotFound(); // Zwróć błąd 404, jeśli danie nie istnieje
            }

            var viewModel = new DishDetailsViewModel
            {
                Dish = dish,
                Reviews = GetMockReviews() // Pobierz przykładowe recenzje
            };

            return View(viewModel);
        }

        private List<ReviewViewModel> GetMockReviews()
        {
            return new List<ReviewViewModel>
            {
                new ReviewViewModel
                {
                    UserName = "Anna Kowalska",
                    UserAvatarUrl = "https://placehold.co/100x100/E8117F/white?text=A",
                    Rating = 5,
                    Comment = "Absolutnie najlepsza pizza w mieście! Ciasto idealne, składniki świeże. Wrócę na pewno!",
                    DatePosted = DateTime.Now.AddDays(-2)
                },
                new ReviewViewModel
                {
                    UserName = "Jan Nowak",
                    UserAvatarUrl = "https://placehold.co/100x100/1143E8/white?text=J",
                    Rating = 4.5,
                    Comment = "Bardzo smaczne danie, dobrze doprawione. Lokal mógłby być trochę większy, ale jedzenie pierwsza klasa.",
                    DatePosted = DateTime.Now.AddDays(-5)
                },
                 new ReviewViewModel
                {
                    UserName = "Katarzyna Wiśniewska",
                    UserAvatarUrl = "https://placehold.co/100x100/E8C311/white?text=K",
                    Rating = 4,
                    Comment = "Dobre, ale jadłam lepsze. Trochę za długi czas oczekiwania.",
                    DatePosted = DateTime.Now.AddDays(-10)
                }
            };
        }

        private List<DishViewModel> GetMockDishes()
        {
            return new List<DishViewModel>
            {
                new DishViewModel { Id = 1, Name = "Pizza Diavola", RestaurantName = "Pizzeria Roma", Price = 38.00m, AverageRating = 4.8, ReviewCount = 45, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza", Tags = new List<string> { "Ostre" }, Description="Klasyczna włoska pizza z pikantnym salami, papryczkami chili i sosem pomidorowym." },
                new DishViewModel { Id = 2, Name = "Klasyczny Burger Wołowy", RestaurantName = "Burgerownia Stacja", Price = 42.00m, AverageRating = 4.9, ReviewCount = 120, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Burger", Tags = new List<string>(), Description="Soczysty kotlet wołowy (200g) w maślanej bułce z sałatą, pomidorem, ogórkiem i naszym autorskim sosem." },
                new DishViewModel { Id = 3, Name = "Zestaw Sushi Ebi Ten", RestaurantName = "Sushi Master", Price = 55.00m, AverageRating = 4.7, ReviewCount = 78, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi", Tags = new List<string> { "Owoce morza" }, Description="Zestaw sushi składający się z 8 kawałków futomaki z krewetką w tempurze, ogórkiem i sosem spicy mayo." },
                new DishViewModel { Id = 4, Name = "Tantanmen Ramen", RestaurantName = "Ramen-Ya", Price = 45.00m, AverageRating = 4.9, ReviewCount = 95, ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Ramen", Tags = new List<string> { "Ostre", "Wegańskie" }, Description="Wegański ramen na kremowym bulionie sezamowym z 'mielonym' z tofu, pak choi i olejem chili." }
            };
        }
    }
}