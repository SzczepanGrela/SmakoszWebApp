// SmakoszWebApp/ViewModels/DishViewModel.cs
using System;
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class DishViewModel
    {
        // Database fields
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int RestaurantId { get; set; }
        public decimal Price { get; set; }
        public string? Description { get; set; }
        public bool IsAvailable { get; set; } = true;
        public int? Calories { get; set; }
        public string ImageUrl { get; set; } = null!;
        public DateTime? CreatedAt { get; set; }

        // Display properties (from joins and calculations)
        public string RestaurantName { get; set; } = null!;
        public double AverageRating { get; set; }          // Calculated from Reviews
        public int ReviewCount { get; set; }               // Calculated from Reviews
        public List<string> Tags { get; set; } = new();    // From Dish_Tags join
    }
}