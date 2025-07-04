// SmakoszWebApp/ViewModels/ProfileViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class ProfileViewModel
    {
        public string UserName { get; set; }
        public string UserAvatarUrl { get; set; }
        public int TotalReviews { get; set; }
        public int TotalPhotos { get; set; }

        public List<DishViewModel> MyRatedDishes { get; set; }
        public List<DishViewModel> SavedForLaterDishes { get; set; }
    }
}