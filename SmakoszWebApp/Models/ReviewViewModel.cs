// SmakoszWebApp/ViewModels/ReviewViewModel.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmakoszWebApp.ViewModels
{
    public class ReviewViewModel
    {
        // Database IDs
        public int Id { get; set; }
        public int UserId { get; set; }
        public int RestaurantId { get; set; }
        public int DishId { get; set; }

        // Multi-dimensional ratings (1-10 scale, matching database schema)
        public int DishRating { get; set; }           // Required - rating of the dish
        public int? ServiceRating { get; set; }       // Optional - rating of service
        public int? CleanlinessRating { get; set; }   // Optional - rating of cleanliness
        public int? AmbianceRating { get; set; }      // Optional - rating of ambiance

        // Review content
        public string? ReviewTitle { get; set; }
        public string Comment { get; set; } = null!;
        public int HelpfulCount { get; set; }
        public bool IsApproved { get; set; } = true;

        // Dates
        public DateTime ReviewDate { get; set; }

        // Display properties (from joins with other tables)
        public string UserName { get; set; } = null!;
        public string? UserAvatarUrl { get; set; }
        public string DishName { get; set; } = null!;
        public string RestaurantName { get; set; } = null!;

        // Computed property for backward compatibility and display
        public double Rating => DishRating;

        // Computed property for overall average including all ratings
        public double AverageOverallRating
        {
            get
            {
                var ratings = new List<int> { DishRating };
                if (ServiceRating.HasValue) ratings.Add(ServiceRating.Value);
                if (CleanlinessRating.HasValue) ratings.Add(CleanlinessRating.Value);
                if (AmbianceRating.HasValue) ratings.Add(AmbianceRating.Value);
                return ratings.Average();
            }
        }
    }
}