// SmakoszWebApp/ViewModels/HomePageViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class HomePageViewModel
    {
        public List<string> PopularCategories { get; set; }
        public List<DishViewModel> HighlyRatedDishes { get; set; }
        public List<DishViewModel> RecommendedDishes { get; set; }
        public List<DishViewModel> NewDiscoveries { get; set; }
        public List<string> QuickChoices { get; set; }
        public List<ReviewViewModel> RecentReviews { get; set; }
        public HomePageStats Stats { get; set; }
        public bool IsUserLoggedIn { get; set; }
        public List<object> AllDishesForRandom { get; set; }
    }

    public class HomePageStats
    {
        public int TotalDishes { get; set; }
        public int TotalRestaurants { get; set; }
        public int TotalReviews { get; set; }
        public int ActiveUsers { get; set; }
    }
}