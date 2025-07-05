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
                         new DishViewModel { Id = 1, Name = "Pizza Diavola", RestaurantName = "Pizzeria Roma", RestaurantId = 1, Price = 38.00m, AverageRating = 4.8, ReviewCount = 45, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza" },
                         new DishViewModel { Id = 15, Name = "Pizza Quattro Formaggi", RestaurantName = "Pizzeria Roma", RestaurantId = 1, Price = 41.00m, AverageRating = 4.6, ReviewCount = 167, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza" }
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
                        new DishViewModel { Id = 2, Name = "Klasyczny Burger Wołowy", RestaurantName = "Burgerownia Stacja", RestaurantId = 2, Price = 42.00m, AverageRating = 4.9, ReviewCount = 120, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Burger" },
                        new DishViewModel { Id = 16, Name = "Fish & Chips Classic", RestaurantName = "Burgerownia Stacja", RestaurantId = 2, Price = 38.00m, AverageRating = 4.5, ReviewCount = 189, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Fish" }
                    }
                },
                new RestaurantViewModel
                {
                    Id = 3,
                    Name = "Sushi Master",
                    City = "Rzeszów",
                    Address = "ul. Słowackiego 8",
                    AverageRating = 4.7,
                    Description = "Autentyczne japońskie sushi przygotowywane przez doświadczonych mistrzów sushi.",
                    ImageUrl = "https://placehold.co/1200x400/2196F3/white?text=Sushi+Master",
                    Dishes = new List<DishViewModel>
                    {
                        new DishViewModel { Id = 3, Name = "Zestaw Sushi Ebi Ten", RestaurantName = "Sushi Master", RestaurantId = 3, Price = 55.00m, AverageRating = 4.7, ReviewCount = 78, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi" },
                        new DishViewModel { Id = 11, Name = "Sushi Dragon Roll", RestaurantName = "Sushi Master", RestaurantId = 3, Price = 65.00m, AverageRating = 4.8, ReviewCount = 156, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi" },
                        new DishViewModel { Id = 17, Name = "Sushi Salmon", RestaurantName = "Sushi Master", RestaurantId = 3, Price = 32.00m, AverageRating = 4.7, ReviewCount = 145, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi" }
                    }
                },
                new RestaurantViewModel
                {
                    Id = 4,
                    Name = "Ramen-Ya",
                    City = "Rzeszów",
                    Address = "ul. Kościuszki 12",
                    AverageRating = 4.8,
                    Description = "Pierwszy bar ramen w Rzeszowie. Serwujemy tradycyjne japońskie zupy ramen z domowym makaronem.",
                    ImageUrl = "https://placehold.co/1200x400/FFC107/white?text=Ramen-Ya",
                    Dishes = new List<DishViewModel>
                    {
                        new DishViewModel { Id = 4, Name = "Tantanmen Ramen", RestaurantName = "Ramen-Ya", RestaurantId = 4, Price = 45.00m, AverageRating = 4.9, ReviewCount = 95, ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Ramen" }
                    }
                },
                new RestaurantViewModel
                {
                    Id = 5,
                    Name = "Noodle House Tokyo",
                    City = "Rzeszów",
                    Address = "ul. Mickiewicza 22",
                    AverageRating = 4.5,
                    Description = "Specjalizujemy się w tradycyjnych japońskich potrawach z makaronem i azjatyckich fusion.",
                    ImageUrl = "https://placehold.co/1200x400/E91E63/white?text=Tokyo+House",
                    Dishes = new List<DishViewModel>
                    {
                        new DishViewModel { Id = 10, Name = "Ramen Shoyu Deluxe", RestaurantName = "Noodle House Tokyo", RestaurantId = 5, Price = 48.00m, AverageRating = 4.9, ReviewCount = 87, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Ramen" },
                        new DishViewModel { Id = 19, Name = "Pho Bo Vietnam", RestaurantName = "Noodle House Tokyo", RestaurantId = 5, Price = 35.00m, AverageRating = 4.8, ReviewCount = 178, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Pho" }
                    }
                },
                new RestaurantViewModel
                {
                    Id = 6,
                    Name = "Thai Garden",
                    City = "Rzeszów",
                    Address = "ul. Szopena 5",
                    AverageRating = 4.6,
                    Description = "Autentyczne smaki Tajlandii w sercu Rzeszowa. Świeże zioła i przyprawy prosto z Tajlandii.",
                    ImageUrl = "https://placehold.co/1200x400/4CAF50/white?text=Thai+Garden",
                    Dishes = new List<DishViewModel>
                    {
                        new DishViewModel { Id = 12, Name = "Pad Thai Premium", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 42.00m, AverageRating = 4.7, ReviewCount = 203, ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Thai" },
                        new DishViewModel { Id = 18, Name = "Tacos Al Pastor", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 28.00m, AverageRating = 4.6, ReviewCount = 201, ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Tacos" },
                        new DishViewModel { Id = 20, Name = "Biryani Chicken", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 39.00m, AverageRating = 4.7, ReviewCount = 167, ImageUrl = "https://placehold.co/600x400/FF9800/white?text=Biryani" }
                    }
                },
                new RestaurantViewModel
                {
                    Id = 7,
                    Name = "Italiano",
                    City = "Rzeszów",
                    Address = "ul. Rejtana 18",
                    AverageRating = 4.7,
                    Description = "Klasyczna kuchnia włoska z nutą nowoczesności. Domowe makarony i świeże składniki.",
                    ImageUrl = "https://placehold.co/1200x400/FF5722/white?text=Italiano",
                    Dishes = new List<DishViewModel>
                    {
                        new DishViewModel { Id = 13, Name = "Carbonara Truffle", RestaurantName = "Italiano", RestaurantId = 7, Price = 52.00m, AverageRating = 4.9, ReviewCount = 98, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Pasta" },
                        new DishViewModel { Id = 21, Name = "Gyros Greek", RestaurantName = "Italiano", RestaurantId = 7, Price = 26.00m, AverageRating = 4.5, ReviewCount = 198, ImageUrl = "https://placehold.co/600x400/9C27B0/white?text=Gyros" }
                    }
                },
                new RestaurantViewModel
                {
                    Id = 8,
                    Name = "Grill Masters",
                    City = "Rzeszów",
                    Address = "ul. Piłsudskiego 30",
                    AverageRating = 4.8,
                    Description = "Specjaliści od grillowanych mięs i BBQ. Najlepsze żeberka i burgery w mieście.",
                    ImageUrl = "https://placehold.co/1200x400/795548/white?text=Grill+Masters",
                    Dishes = new List<DishViewModel>
                    {
                        new DishViewModel { Id = 14, Name = "Burger BBQ Supreme", RestaurantName = "Grill Masters", RestaurantId = 8, Price = 45.00m, AverageRating = 4.8, ReviewCount = 234, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=BBQ" },
                        new DishViewModel { Id = 22, Name = "Kebab Turkish", RestaurantName = "Grill Masters", RestaurantId = 8, Price = 24.00m, AverageRating = 4.4, ReviewCount = 234, ImageUrl = "https://placehold.co/600x400/795548/white?text=Kebab" }
                    }
                }
            };
        }
    }
}