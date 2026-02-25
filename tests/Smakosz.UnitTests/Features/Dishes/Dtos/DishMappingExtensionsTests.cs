using FluentAssertions;
using Smakosz.Application.Features.Dishes.Dtos;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Dishes.Dtos;

[Trait("Category", "Mapping")]
public class DishMappingExtensionsTests
{
    [Fact]
    public void ToCardDto_AllFields_MapsCorrectly()
    {
        var restaurant = new RestaurantBuilder().WithName("Bella Italia").WithSlug("bella-italia").Build();
        var dish = new DishBuilder()
            .WithRestaurant(restaurant)
            .AsVegetarian()
            .AsGlutenFree()
            .Build();

        var dto = dish.ToCardDto(true);

        dto.PublicId.Should().Be(dish.PublicId);
        dto.Slug.Should().Be(dish.Slug);
        dto.DishName.Should().Be(dish.DishName);
        dto.Price.Should().Be(dish.Price);
        dto.AvgRating.Should().Be(dish.AvgRating);
        dto.ReviewCount.Should().Be(dish.ReviewCount);
        dto.ImageUrl.Should().Be(dish.ImageUrl);
        dto.RestaurantName.Should().Be("Bella Italia");
        dto.RestaurantSlug.Should().Be("bella-italia");
        dto.IsVegetarian.Should().BeTrue();
        dto.IsGlutenFree.Should().BeTrue();
        dto.IsSaved.Should().BeTrue();
    }

    [Fact]
    public void ToCardDto_NullRestaurant_RestaurantFieldsNull()
    {
        var dish = new DishBuilder().Build();
        dish.Restaurant = null;

        var dto = dish.ToCardDto(false);

        dto.RestaurantName.Should().BeNull();
        dto.RestaurantSlug.Should().BeNull();
    }

    [Fact]
    public void ToCardDto_IsSavedFalse_MapsCorrectly()
    {
        var dish = new DishBuilder().Build();

        var dto = dish.ToCardDto(false);

        dto.IsSaved.Should().BeFalse();
    }

    [Fact]
    public void ToDetailDto_AllFields_MapsCorrectly()
    {
        var city = new City { CityId = 1, CityName = "Gdansk" };
        var restaurant = new RestaurantBuilder()
            .WithCity(city)
            .WithCuisineType("Polish")
            .Build();
        var dish = new DishBuilder()
            .WithRestaurant(restaurant)
            .AsVegan()
            .AsSpicy()
            .AsLactoseFree()
            .Build();

        var dto = dish.ToDetailDto(true);

        dto.PublicId.Should().Be(dish.PublicId);
        dto.Description.Should().Be(dish.Description);
        dto.Calories.Should().Be(dish.Calories);
        dto.IsVegetarian.Should().BeTrue(); // AsVegan sets both
        dto.IsVegan.Should().BeTrue();
        dto.IsSpicy.Should().BeTrue();
        dto.IsLactoseFree.Should().BeTrue();
        dto.IsAvailable.Should().Be(dish.IsAvailable);
        dto.TrendingScore.Should().Be(dish.TrendingScore);
        dto.RestaurantName.Should().Be(restaurant.RestaurantName);
        dto.CuisineType.Should().Be("Polish");
        dto.CityName.Should().Be("Gdansk");
        dto.IsSaved.Should().BeTrue();
    }

    [Fact]
    public void ToDetailDto_NullRestaurant_NestedFieldsNull()
    {
        var dish = new DishBuilder().Build();
        dish.Restaurant = null;

        var dto = dish.ToDetailDto(false);

        dto.RestaurantName.Should().BeNull();
        dto.RestaurantSlug.Should().BeNull();
        dto.CuisineType.Should().BeNull();
        dto.CityName.Should().BeNull();
    }

    [Fact]
    public void ToDetailDto_RestaurantWithoutCity_CityNameNull()
    {
        var restaurant = new RestaurantBuilder().Build();
        restaurant.City = null;
        var dish = new DishBuilder().WithRestaurant(restaurant).Build();

        var dto = dish.ToDetailDto(false);

        dto.RestaurantName.Should().Be(restaurant.RestaurantName);
        dto.CityName.Should().BeNull();
    }
}
