// SmakoszWebApp/ViewModels/DishDetailsViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class DishDetailsViewModel
    {
        public DishViewModel Dish { get; set; } = null!;
        public List<ReviewViewModel> Reviews { get; set; } = new();
        public RestaurantViewModel? Restaurant { get; set; }
    }
}