// SmakoszWebApp/ViewModels/RestaurantViewModel.cs
using System.Collections.Generic;
using System.Globalization;

namespace SmakoszWebApp.ViewModels
{
    public class RestaurantViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string City { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string? Description { get; set; }
        public double AverageRating { get; set; }
        public string? ImageUrl { get; set; }

        // Location properties
        public double Latitude { get; set; }
        public double Longitude { get; set; }

        // Contact information
        public string? Phone { get; set; }
        public string? Website { get; set; }

        // Lista dań serwowanych w tej restauracji
        public List<DishViewModel>? Dishes { get; set; }
        
        // Helper property for Google Maps URL
        public string GoogleMapsUrl => $"https://www.google.com/maps?q={Latitude.ToString(CultureInfo.InvariantCulture)},{Longitude.ToString(CultureInfo.InvariantCulture)}";
        
        // Helper property for full address
        public string FullAddress => $"{Address}, {City}";
    }
}