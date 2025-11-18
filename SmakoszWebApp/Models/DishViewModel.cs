// SmakoszWebApp/ViewModels/DishViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class DishViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string RestaurantName { get; set; } = null!;
        public int RestaurantId { get; set; }
        public decimal Price { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public string ImageUrl { get; set; } = null!;
        public List<string> Tags { get; set; } = new(); // np. ["Ostre", "Wegańskie"]
        public string? Description { get; set; }
    }
}