// SmakoszWebApp/ViewModels/ProfileViewModel.cs
using System;
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class ProfileViewModel
    {
        // User database fields
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? UserAvatarUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? HomeCity { get; set; }
        public DateTime? AccountCreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Statistics
        public int TotalReviews { get; set; }
        public int TotalPhotos { get; set; }

        // User content
        public List<ReviewViewModel> MyRatedDishes { get; set; } = new();
        public List<DishViewModel> SavedForLaterDishes { get; set; } = new();
    }
}