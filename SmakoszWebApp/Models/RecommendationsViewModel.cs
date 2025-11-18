// Models/RecommendationsViewModel.cs
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace SmakoszWebApp.ViewModels
{
    public class RecommendationsViewModel
    {
        public List<DishViewModel> PersonalizedRecommendations { get; set; } = new();
        public List<DishViewModel> TrendingDishes { get; set; } = new();
        public List<DishViewModel> SimilarUsersRecommendations { get; set; } = new();
    }
}

// Models/AdminDashboardViewModel.cs
namespace SmakoszWebApp.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalReviews { get; set; }
        public int TotalDishes { get; set; }
        public int TotalRestaurants { get; set; }
        public List<PendingPhotoViewModel> PendingPhotos { get; set; } = new();
        public List<ReportViewModel> RecentReports { get; set; } = new();
    }
}

// Models/PendingPhotoViewModel.cs
namespace SmakoszWebApp.ViewModels
{
    public class PendingPhotoViewModel
    {
        public int Id { get; set; }
        public string DishName { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public DateTime UploadedDate { get; set; }
        public string ImageUrl { get; set; } = null!;
    }
}

// Models/ReportViewModel.cs
namespace SmakoszWebApp.ViewModels
{
    public class ReportViewModel
    {
        public int Id { get; set; }
        public string Type { get; set; } = null!;
        public string ReportedContent { get; set; } = null!;
        public string ReportedBy { get; set; } = null!;
        public DateTime ReportDate { get; set; }
    }
}

// Models/AddReviewViewModel.cs
namespace SmakoszWebApp.ViewModels
{
    public class AddReviewViewModel
    {
        public int DishId { get; set; }
        public string? DishName { get; set; }
        public string? RestaurantName { get; set; }

        // Multi-dimensional ratings (1-10 scale, matching database schema)
        public int DishRating { get; set; }           // Required - rating of the dish (1-10)
        public int? ServiceRating { get; set; }       // Optional - rating of service (1-10)
        public int? CleanlinessRating { get; set; }   // Optional - rating of cleanliness (1-10)
        public int? AmbianceRating { get; set; }      // Optional - rating of ambiance (1-10)

        // Review content
        public string? ReviewTitle { get; set; }
        public string Comment { get; set; } = null!;
        public List<IFormFile> Photos { get; set; } = new();

        // Backward compatibility - maps to DishRating
        public int Rating
        {
            get => DishRating;
            set => DishRating = value;
        }
    }
}