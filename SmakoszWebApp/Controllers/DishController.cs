// Controllers/DishController.cs
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

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

            // Pobierz informacje o restauracji
            var restaurant = GetMockRestaurants().FirstOrDefault(r => r.Id == dish.RestaurantId);
            
            var viewModel = new DishDetailsViewModel
            {
                Dish = dish,
                Reviews = GetMockReviews(), // Pobierz przykładowe recenzje
                Restaurant = restaurant != null ? new RestaurantLocationInfo
                {
                    Id = restaurant.Id,
                    Name = restaurant.Name,
                    Address = restaurant.Address,
                    City = restaurant.City,
                    Latitude = restaurant.Latitude,
                    Longitude = restaurant.Longitude,
                    Phone = restaurant.Phone,
                    Website = restaurant.Website
                } : null
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
                
                // Dania z rekomendacji strony głównej
                new DishViewModel { Id = 5, Name = "Ramen Tonkotsu Special", RestaurantName = "Ramen-Ya", RestaurantId = 4, Price = 52.00m, AverageRating = 4.9, ReviewCount = 156, ImageUrl = "https://placehold.co/400x300/FFC107/white?text=Ramen+Special", Tags = new List<string> { "Polecane", "Nowe" }, Description="Specjalny ramen tonkotsu z kremowym bulionem, chashu i marynowanym jajkiem." },
                new DishViewModel { Id = 6, Name = "Neapolitan Pizza Margherita", RestaurantName = "Pizzeria Italiana", RestaurantId = 5, Price = 36.00m, AverageRating = 4.8, ReviewCount = 203, ImageUrl = "https://placehold.co/400x300/ff6f61/white?text=Margherita", Tags = new List<string> { "Klasyka", "Wegetariańskie" }, Description="Klasyczna neapolitańska margherita z San Marzano, mozzarellą di bufala i świeżą bazylią." },
                new DishViewModel { Id = 7, Name = "Pad Thai Chicken", RestaurantName = "Thai Smile", RestaurantId = 6, Price = 41.00m, AverageRating = 4.7, ReviewCount = 89, ImageUrl = "https://placehold.co/400x300/FF9800/white?text=Pad+Thai", Tags = new List<string> { "Ostre", "Azjatyckie" }, Description="Autentyczny pad thai z kurczakiem, kiełkami fasoli, orzeszkami i sosem tamaryndowym." },
                
                // Dania z nowych odkryć
                new DishViewModel { Id = 8, Name = "Korean BBQ Bibimbap", RestaurantName = "Seoul Kitchen", RestaurantId = 7, Price = 47.00m, AverageRating = 4.8, ReviewCount = 34, ImageUrl = "https://placehold.co/300x200/E91E63/white?text=Korean", Tags = new List<string> { "Nowe", "Koreańskie" }, Description="Tradycyjny bibimbap z grillowanym mięsem, świeżymi warzywami i kimchi." },
                new DishViewModel { Id = 9, Name = "Truffle Pasta Carbonara", RestaurantName = "Pasta Perfetta", RestaurantId = 8, Price = 67.00m, AverageRating = 4.9, ReviewCount = 23, ImageUrl = "https://placehold.co/300x200/8BC34A/white?text=Truffle", Tags = new List<string> { "Nowe", "Luksusowe" }, Description="Luksusowa carbonara z czarnymi truflami, boczkiem guanciale i parmezanem Reggiano." },
                
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

        private List<RestaurantViewModel> GetMockRestaurants()
        {
            return new List<RestaurantViewModel>
            {
                new RestaurantViewModel { Id = 1, Name = "Pizzeria Roma", City = "Rzeszów", Address = "ul. Grunwaldzka 1", Latitude = 50.0377, Longitude = 21.9993, Phone = "+48 17 853 12 34", Website = "https://pizzeriaroma-rzeszow.pl" },
                new RestaurantViewModel { Id = 2, Name = "Burgerownia Stacja", City = "Rzeszów", Address = "ul. 3 Maja 15", Latitude = 50.0394, Longitude = 21.9990, Phone = "+48 17 862 45 67", Website = "https://burgerownia-stacja.pl" },
                new RestaurantViewModel { Id = 3, Name = "Sushi Master", City = "Rzeszów", Address = "ul. Słowackiego 8", Latitude = 50.0347, Longitude = 22.0025, Phone = "+48 17 864 78 90", Website = "https://sushimaster-rzeszow.pl" },
                new RestaurantViewModel { Id = 4, Name = "Ramen-Ya", City = "Rzeszów", Address = "ul. Kościuszki 12", Latitude = 50.0381, Longitude = 22.0021, Phone = "+48 17 855 32 10", Website = "https://ramen-ya.pl" },
                new RestaurantViewModel { Id = 5, Name = "Noodle House Tokyo", City = "Rzeszów", Address = "ul. Mickiewicza 22", Latitude = 50.0365, Longitude = 22.0010, Phone = "+48 17 867 54 32", Website = "https://noodlehouse-tokyo.pl" },
                new RestaurantViewModel { Id = 6, Name = "Thai Garden", City = "Rzeszów", Address = "ul. Szopena 5", Latitude = 50.0401, Longitude = 22.0045, Phone = "+48 17 856 98 76", Website = "https://thai-garden.pl" },
                new RestaurantViewModel { Id = 7, Name = "Italiano", City = "Rzeszów", Address = "ul. Rejtana 18", Latitude = 50.0425, Longitude = 22.0018, Phone = "+48 17 858 76 54", Website = "https://italiano-rzeszow.pl" },
                new RestaurantViewModel { Id = 8, Name = "Grill Masters", City = "Rzeszów", Address = "ul. Piłsudskiego 30", Latitude = 50.0358, Longitude = 22.0038, Phone = "+48 17 859 43 21", Website = "https://grillmasters.pl" }
            };
        }
    }
}