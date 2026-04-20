using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Dishes.Dtos;

public static class DishMappingExtensions
{
    public static DishCardDto ToCardDto(this Dish d, bool isSaved)
    {
        return new DishCardDto
        {
            PublicId = d.PublicId,
            Slug = d.Slug ?? string.Empty,
            DishName = d.DishName,
            Price = d.Price,
            AvgRating = d.AvgRating,
            ReviewCount = d.ReviewCount,
            ImageUrl = d.ImageUrl,
            ImageBlurhash = d.ImageBlurhash,
            RestaurantName = d.Restaurant?.RestaurantName,
            RestaurantSlug = d.Restaurant?.Slug,
            IsVegetarian = d.IsVegetarian,
            IsVegan = d.IsVegan,
            IsGlutenFree = d.IsGlutenFree,
            IsSpicy = d.IsSpicy,
            IsSaved = isSaved
        };
    }

    public static DishDetailDto ToDetailDto(this Dish d, bool isSaved)
    {
        return new DishDetailDto
        {
            PublicId = d.PublicId,
            Slug = d.Slug ?? string.Empty,
            DishName = d.DishName,
            Price = d.Price,
            AvgRating = d.AvgRating,
            ReviewCount = d.ReviewCount,
            ImageUrl = d.ImageUrl,
            ImageBlurhash = d.ImageBlurhash,
            Description = d.Description,
            Calories = d.Calories,
            IngredientsJson = d.IngredientsJson,
            IsVegetarian = d.IsVegetarian,
            IsVegan = d.IsVegan,
            IsGlutenFree = d.IsGlutenFree,
            IsLactoseFree = d.IsLactoseFree,
            IsSpicy = d.IsSpicy,
            IsAvailable = d.IsAvailable,
            TrendingScore = d.TrendingScore,
            RestaurantName = d.Restaurant?.RestaurantName,
            RestaurantSlug = d.Restaurant?.Slug,
            CuisineType = d.Restaurant?.Cuisine?.DisplayName,
            CityName = d.Restaurant?.City?.CityName,
            IsSaved = isSaved,
            Tags = d.DishTags.Select(dt => new TagDto
            {
                TagName = dt.Tag.TagName,
                Category = dt.Tag.Category,
                DisplayColor = dt.Tag.DisplayColor
            }).ToList(),
            Ingredients = d.DishIngredients.Select(di => new DishIngredientDto
            {
                IngredientId = di.Ingredient.IngredientId,
                Name = di.Ingredient.IngredientName,
                IconUrl = di.Ingredient.IconUrl,
                IconBlurhash = di.Ingredient.IconBlurhash,
                IsAllergen = di.Ingredient.IsAllergen,
                IsVegetarian = di.Ingredient.IsVegetarian,
                IsVegan = di.Ingredient.IsVegan,
                IsGlutenFree = di.Ingredient.IsGlutenFree,
                IsLactoseFree = di.Ingredient.IsLactoseFree
            }).ToList()
        };
    }
}
