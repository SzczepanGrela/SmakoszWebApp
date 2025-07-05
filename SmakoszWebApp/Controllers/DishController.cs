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
                    Id = 1,
                    UserName = "Anna Kowalska",
                    UserAvatarUrl = "https://placehold.co/100x100/E8117F/white?text=A",
                    Rating = 5,
                    Comment = "Absolutnie najlepsza pizza w mieście! Ciasto idealne, składniki świeże. Wrócę na pewno!",
                    DatePosted = DateTime.Now.AddDays(-2)
                },
                new ReviewViewModel
                {
                    Id = 2,
                    UserName = "Jan Nowak",
                    UserAvatarUrl = "https://placehold.co/100x100/1143E8/white?text=J",
                    Rating = 4.5,
                    Comment = "Bardzo smaczne danie, dobrze doprawione. Lokal mógłby być trochę większy, ale jedzenie pierwsza klasa.",
                    DatePosted = DateTime.Now.AddDays(-5)
                },
                 new ReviewViewModel
                {
                    Id = 3,
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
                new DishViewModel { Id = 1, Name = "Pizza Diavola", RestaurantName = "Pizzeria Roma", RestaurantId = 1, Price = 38.00m, AverageRating = 4.8, ReviewCount = 45, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza", Tags = new List<string> { "Ostre" }, Description="Klasyczna włoska pizza z pikantnym salami, papryczkami chili i sosem pomidorowym." },
                new DishViewModel { Id = 2, Name = "Klasyczny Burger Wołowy", RestaurantName = "Burgerownia Stacja", RestaurantId = 2, Price = 42.00m, AverageRating = 4.9, ReviewCount = 120, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Burger", Tags = new List<string>(), Description="Soczysty kotlet wołowy (200g) w maślanej bułce z sałatą, pomidorem, ogórkiem i naszym autorskim sosem." },
                new DishViewModel { Id = 3, Name = "Zestaw Sushi Ebi Ten", RestaurantName = "Sushi Master", RestaurantId = 3, Price = 55.00m, AverageRating = 4.7, ReviewCount = 78, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi", Tags = new List<string> { "Owoce morza" }, Description="Zestaw sushi składający się z 8 kawałków futomaki z krewetką w tempurze, ogórkiem i sosem spicy mayo." },
                new DishViewModel { Id = 4, Name = "Tantanmen Ramen", RestaurantName = "Ramen-Ya", RestaurantId = 4, Price = 45.00m, AverageRating = 4.9, ReviewCount = 95, ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Ramen", Tags = new List<string> { "Ostre", "Wegańskie" }, Description="Wegański ramen na kremowym bulionie sezamowym z 'mielonym' z tofu, pak choi i olejem chili." },
                
                // Dodatkowe dania z rekomendacji
                new DishViewModel { Id = 10, Name = "Ramen Shoyu Deluxe", RestaurantName = "Noodle House Tokyo", RestaurantId = 5, Price = 48.00m, AverageRating = 4.9, ReviewCount = 87, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Ramen", Description="Tradycyjny ramen shoyu z domowym makaronem i aromatycznym bulionem." },
                new DishViewModel { Id = 11, Name = "Sushi Dragon Roll", RestaurantName = "Sushi Master", RestaurantId = 3, Price = 65.00m, AverageRating = 4.8, ReviewCount = 156, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi", Description="Ekskluzywny roll z węgorzem, krewetką i awokado, podany z sosem unagi." },
                new DishViewModel { Id = 12, Name = "Pad Thai Premium", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 42.00m, AverageRating = 4.7, ReviewCount = 203, ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Thai", Description="Autentyczny pad thai z krewetkami, kiełkami bambusa i orzeszkami ziemnymi." },
                new DishViewModel { Id = 13, Name = "Carbonara Truffle", RestaurantName = "Italiano", RestaurantId = 7, Price = 52.00m, AverageRating = 4.9, ReviewCount = 98, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Pasta", Description="Kremowa carbonara z truflami, boczkiem guanciale i świeżym parmezanem." },
                new DishViewModel { Id = 14, Name = "Burger BBQ Supreme", RestaurantName = "Grill Masters", RestaurantId = 8, Price = 45.00m, AverageRating = 4.8, ReviewCount = 234, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=BBQ", Description="Potężny burger z kotletem BBQ, bekonem, cebulą karmelizowaną i sosem BBQ." },
                new DishViewModel { Id = 15, Name = "Pizza Quattro Formaggi", RestaurantName = "Pizzeria Roma", RestaurantId = 1, Price = 41.00m, AverageRating = 4.6, ReviewCount = 167, ImageUrl = "https://placehold.co/600x400/ff6f61/white?text=Pizza", Description="Pizza z czterema serami: mozzarella, gorgonzola, parmezan i pecorino." },
                new DishViewModel { Id = 16, Name = "Fish & Chips Classic", RestaurantName = "Burgerownia Stacja", RestaurantId = 2, Price = 38.00m, AverageRating = 4.5, ReviewCount = 189, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Fish", Description="Klasyczne fish & chips z rybą w cieście piwnym i domowymi frytkami." },
                new DishViewModel { Id = 17, Name = "Sushi Salmon", RestaurantName = "Sushi Master", RestaurantId = 3, Price = 32.00m, AverageRating = 4.7, ReviewCount = 145, ImageUrl = "https://placehold.co/600x400/2196F3/white?text=Sushi", Description="Świeży łosoś na sushi rice z wasabi i marynowanym imbirem." },
                new DishViewModel { Id = 18, Name = "Tacos Al Pastor", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 28.00m, AverageRating = 4.6, ReviewCount = 201, ImageUrl = "https://placehold.co/600x400/FFC107/white?text=Tacos", Description="Meksykańskie tacos z marynowaną wieprzowiną, ananasem i cebulą." },
                new DishViewModel { Id = 19, Name = "Pho Bo Vietnam", RestaurantName = "Noodle House Tokyo", RestaurantId = 5, Price = 35.00m, AverageRating = 4.8, ReviewCount = 178, ImageUrl = "https://placehold.co/600x400/4CAF50/white?text=Pho", Description="Tradycyjna wietnamska zupa pho z wołowiną i świeżymi ziołami." },
                new DishViewModel { Id = 20, Name = "Biryani Chicken", RestaurantName = "Thai Garden", RestaurantId = 6, Price = 39.00m, AverageRating = 4.7, ReviewCount = 167, ImageUrl = "https://placehold.co/600x400/FF9800/white?text=Biryani", Description="Aromatyczne biryani z kurczakiem, ryżem basmati i przyprawami." },
                new DishViewModel { Id = 21, Name = "Gyros Greek", RestaurantName = "Italiano", RestaurantId = 7, Price = 26.00m, AverageRating = 4.5, ReviewCount = 198, ImageUrl = "https://placehold.co/600x400/9C27B0/white?text=Gyros", Description="Grecki gyros z wieprzowiną, tzatziki, pomidorami i czerwoną cebulą." },
                new DishViewModel { Id = 22, Name = "Kebab Turkish", RestaurantName = "Grill Masters", RestaurantId = 8, Price = 24.00m, AverageRating = 4.4, ReviewCount = 234, ImageUrl = "https://placehold.co/600x400/795548/white?text=Kebab", Description="Turecki kebab z mięsem z rożna, świeżymi warzywami i sosem czosnkowym." }
            };
        }
    }
}