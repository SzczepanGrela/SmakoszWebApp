// SmakoszWebApp/ViewModels/ReviewViewModel.cs
using System;

namespace SmakoszWebApp.ViewModels
{
    public class ReviewViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        public string UserAvatarUrl { get; set; }
        public double Rating { get; set; }
        public string Comment { get; set; }
        public DateTime Date { get; set; }
        public DateTime DatePosted { get; set; }
        public string DishName { get; set; }
        public string RestaurantName { get; set; }
        public int DishId { get; set; }
    }
}