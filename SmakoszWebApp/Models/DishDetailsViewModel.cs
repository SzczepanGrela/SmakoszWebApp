// SmakoszWebApp/ViewModels/DishDetailsViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class DishDetailsViewModel
    {
        public DishViewModel Dish { get; set; }
        public List<ReviewViewModel> Reviews { get; set; }
        public RestaurantViewModel Restaurant { get; set; }
    }
}