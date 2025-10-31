// SmakoszWebApp/ViewModels/SearchResultsViewModel.cs
using System.Collections.Generic;

namespace SmakoszWebApp.ViewModels
{
    public class SearchResultsViewModel
    {
        public string Query { get; set; }
        public List<DishViewModel> Results { get; set; }
        public int ResultCount => Results?.Count ?? 0;

        // Tutaj w przyszłości znajdą się dane dla filtrów, 
        // np. dostępne typy kuchni, zakresy cenowe itp.
    }
}