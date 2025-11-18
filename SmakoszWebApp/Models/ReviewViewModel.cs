// SmakoszWebApp/ViewModels/ReviewViewModel.cs
using System;

namespace SmakoszWebApp.ViewModels
{
    public class ReviewViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } = null!;
        public string? UserAvatarUrl { get; set; }
        public double Rating { get; set; }
        public string Comment { get; set; } = null!;
        public DateTime Date { get; set; }
        public DateTime DatePosted { get; set; }
        public string DishName { get; set; } = null!;
        public string RestaurantName { get; set; } = null!;
        public int DishId { get; set; }
    }
}