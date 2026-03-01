using Smakosz.Domain.Entities;

namespace Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

public class DishBuilder
{
    private readonly Dish _dish = new()
    {
        DishId = 1,
        PublicId = Guid.NewGuid(),
        DishName = "Test Dish",
        Slug = "test-dish",
        Price = 29.99m,
        AvgRating = 8.0,
        ReviewCount = 5,
        IsAvailable = true,
        IsVegetarian = false,
        IsVegan = false,
        IsGlutenFree = false,
        IsLactoseFree = false,
        IsSpicy = false,
        Description = "A test dish",
        Calories = 500,
        ImageUrl = "https://example.com/dish.jpg",
        TrendingScore = 30m,
        CreatedAt = DateTime.UtcNow
    };

    public DishBuilder WithId(int id) { _dish.DishId = id; return this; }
    public DishBuilder WithPublicId(Guid id) { _dish.PublicId = id; return this; }
    public DishBuilder WithName(string name) { _dish.DishName = name; return this; }
    public DishBuilder WithSlug(string slug) { _dish.Slug = slug; return this; }
    public DishBuilder WithPrice(decimal? price) { _dish.Price = price; return this; }
    public DishBuilder WithAvgRating(double? rating) { _dish.AvgRating = rating; return this; }
    public DishBuilder WithReviewCount(int count) { _dish.ReviewCount = count; return this; }
    public DishBuilder WithRestaurant(Restaurant restaurant) { _dish.Restaurant = restaurant; _dish.RestaurantId = restaurant.RestaurantId; return this; }
    public DishBuilder WithRestaurantId(int? id) { _dish.RestaurantId = id; return this; }
    public DishBuilder WithTrendingScore(decimal? score) { _dish.TrendingScore = score; return this; }
    public DishBuilder AsAvailable() { _dish.IsAvailable = true; return this; }
    public DishBuilder AsUnavailable() { _dish.IsAvailable = false; return this; }
    public DishBuilder AsVegetarian() { _dish.IsVegetarian = true; return this; }
    public DishBuilder AsVegan() { _dish.IsVegan = true; _dish.IsVegetarian = true; return this; }
    public DishBuilder AsGlutenFree() { _dish.IsGlutenFree = true; return this; }
    public DishBuilder AsLactoseFree() { _dish.IsLactoseFree = true; return this; }
    public DishBuilder AsSpicy() { _dish.IsSpicy = true; return this; }

    public DishBuilder WithDishTags(List<DishTag> tags) { _dish.DishTags = tags; return this; }

    public Dish Build() => _dish;
}
