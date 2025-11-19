// Controllers/AdminController.cs
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmakoszWebApp.ViewModels;

namespace SmakoszWebApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            var viewModel = new AdminDashboardViewModel
            {
                TotalUsers = 1234,
                TotalReviews = 5678,
                TotalDishes = 890,
                TotalRestaurants = 123,
                PendingPhotos = GetPendingPhotos(),
                RecentReports = GetRecentReports()
            };
            return View(viewModel);
        }

        private List<PendingPhotoViewModel> GetPendingPhotos()
        {
            return new List<PendingPhotoViewModel>
            {
                new PendingPhotoViewModel
                {
                    Id = 1,
                    DishName = "Pizza Margherita",
                    UserName = "jan.kowalski",
                    UploadedDate = DateTime.Now.AddHours(-2),
                    ImageUrl = "https://placehold.co/200x150/ff6f61/white?text=Pizza"
                },
                new PendingPhotoViewModel
                {
                    Id = 2,
                    DishName = "Burger Classic",
                    UserName = "anna.nowak",
                    UploadedDate = DateTime.Now.AddHours(-4),
                    ImageUrl = "https://placehold.co/200x150/4CAF50/white?text=Burger"
                },
                new PendingPhotoViewModel
                {
                    Id = 3,
                    DishName = "Sushi Roll",
                    UserName = "piotr.kowal",
                    UploadedDate = DateTime.Now.AddHours(-6),
                    ImageUrl = "https://placehold.co/200x150/2196F3/white?text=Sushi"
                }
            };
        }

        private List<ReportViewModel> GetRecentReports()
        {
            return new List<ReportViewModel>
            {
                new ReportViewModel
                {
                    Id = 1,
                    Type = "Nieodpowiedni komentarz",
                    ReportedContent = "Ten komentarz zawiera wulgaryzmy...",
                    ReportedBy = "anna.nowak",
                    ReportDate = DateTime.Now.AddDays(-1)
                },
                new ReportViewModel
                {
                    Id = 2,
                    Type = "Spam",
                    ReportedContent = "Użytkownik dodaje identyczne komentarze...",
                    ReportedBy = "jan.kowalski",
                    ReportDate = DateTime.Now.AddDays(-2)
                }
            };
        }

        [HttpPost]
        public IActionResult ApprovePhoto(int photoId)
        {
            // ✅ FIX: Validate photoId
            if (photoId <= 0)
            {
                return BadRequest("Invalid photo ID.");
            }

            return Json(new { success = true, message = $"Zdjęcie {photoId} zostało zatwierdzone!" });
        }

        [HttpPost]
        public IActionResult RejectPhoto(int photoId)
        {
            // ✅ FIX: Validate photoId
            if (photoId <= 0)
            {
                return BadRequest("Invalid photo ID.");
            }

            return Json(new { success = true, message = $"Zdjęcie {photoId} zostało odrzucone!" });
        }
    }
}