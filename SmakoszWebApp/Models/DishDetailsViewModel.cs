// SmakoszWebApp/ViewModels/DishDetailsViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class DishDetailsViewModel
    {
        public DishViewModel Dish { get; set; }
        public List<ReviewViewModel> Reviews { get; set; }
        public RestaurantLocationInfo Restaurant { get; set; }
    }

    public class RestaurantLocationInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Phone { get; set; }
        public string Website { get; set; }
        
        // Helper property for Google Maps URL
        public string GoogleMapsUrl => $"https://www.google.com/maps?q={Latitude},{Longitude}";
        
        // Helper property for full address
        public string FullAddress => $"{Address}, {City}";
    }
}