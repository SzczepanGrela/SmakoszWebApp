using Smakosz.Application.Features.Restaurants.Dtos;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Application.Features.Reviews.Dtos;

namespace Smakosz.Application.Features.Home.Dtos;

public class HomeDataDto
{
    public required StatsDto Stats { get; init; }
    public required List<RestaurantCardDto> TrendingRestaurants { get; init; }
    public required List<DishCardDto> TrendingDishes { get; init; }
    public required List<DishCardDto> TopRatedDishes { get; init; }
    public required List<ReviewCardDto> RecentReviews { get; init; }
    public required List<PopularCategoryDto> PopularCategories { get; init; }
    public HeroImageDto? HeroImage { get; init; }
}

public class PopularCategoryDto
{
    public string Name { get; init; } = string.Empty;
    public string? Slug { get; init; }
    public string? Icon { get; init; }
}

public class HeroImageDto
{
    public string Url { get; init; } = string.Empty;
    public string? Blurhash { get; init; }
    public string? CreditText { get; init; }
}

public class StatsDto
{
    public int TotalDishes { get; init; }
    public int TotalRestaurants { get; init; }
    public int TotalReviews { get; init; }
}
