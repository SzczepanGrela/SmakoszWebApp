// SmakoszWebApp/ViewModels/HomePageViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class HomePageViewModel
    {
        public List<string> PopularCategories { get; set; }
        public List<DishViewModel> HighlyRatedDishes { get; set; }
    }
}