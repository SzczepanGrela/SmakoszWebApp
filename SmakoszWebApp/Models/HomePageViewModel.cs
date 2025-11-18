// SmakoszWebApp/ViewModels/HomePageViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class HomePageViewModel
    {
        public List<string> PopularCategories { get; set; } = new();
        public List<DishViewModel> HighlyRatedDishes { get; set; } = new();
        public List<DishViewModel> RecommendedDishes { get; set; } = new();
        public List<DishViewModel> NewDiscoveries { get; set; } = new();
        public List<string> QuickChoices { get; set; } = new();
        public List<ReviewViewModel> RecentReviews { get; set; } = new();
        public HomePageStats Stats { get; set; } = null!;
        public bool IsUserLoggedIn { get; set; }
        public List<object> AllDishesForRandom { get; set; } = new();
    }

    public class HomePageStats
    {
        public int TotalDishes { get; set; }
        public int TotalRestaurants { get; set; }
        public int TotalReviews { get; set; }
        public int ActiveUsers { get; set; }
    }
}