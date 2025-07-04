// SmakoszWebApp/ViewModels/RestaurantViewModel.cs
namespace SmakoszWebApp.ViewModels
{
    public class RestaurantViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
        public string Description { get; set; }
        public double AverageRating { get; set; }
        public string ImageUrl { get; set; }

        // Lista dań serwowanych w tej restauracji
        public List<DishViewModel> Dishes { get; set; }
    }
}