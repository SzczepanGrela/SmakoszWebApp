// Models/RecommendationsViewModel.cs
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
        public string DishName { get; set; }
        public string UserName { get; set; }
        public DateTime UploadedDate { get; set; }
        public string ImageUrl { get; set; }
    }
}

// Models/ReportViewModel.cs
namespace SmakoszWebApp.ViewModels
{
    public class ReportViewModel
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string ReportedContent { get; set; }
        public string ReportedBy { get; set; }
        public DateTime ReportDate { get; set; }
    }
}

// Models/AddReviewViewModel.cs
namespace SmakoszWebApp.ViewModels
{
    public class AddReviewViewModel
    {
        public int DishId { get; set; }
        public string DishName { get; set; }
        public string RestaurantName { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
        public List<IFormFile> Photos { get; set; } = new();
    }
}