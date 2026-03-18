using FluentAssertions;
using Smakosz.Application.Features.Restaurants.Dtos;
using Smakosz.Domain.Entities;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Restaurants.Dtos;

[Trait("Category", "Mapping")]
public class RestaurantMappingExtensionsTests
{
    [Fact]
    public void ToCardDto_AllFields_MapsCorrectly()
    {
        var city = new City { CityId = 1, CityName = "Warsaw" };
        var restaurant = new RestaurantBuilder()
            .WithCity(city)
            .WithCuisineType("Italian")
            .WithPriceLevel(3)
            .WithAvgFoodScore(8.5)
            .Build();

        var dto = restaurant.ToCardDto(true, 42);

        dto.PublicId.Should().Be(restaurant.PublicId);
        dto.Slug.Should().Be(restaurant.Slug);
        dto.RestaurantName.Should().Be(restaurant.RestaurantName);
        dto.CuisineType.Should().Be("Italian");
        dto.CityName.Should().Be("Warsaw");
        dto.PriceLevel.Should().Be(3);
        dto.AvgFoodScore.Should().Be(8.5);
        dto.ReviewCount.Should().Be(42);
        dto.ImageUrl.Should().Be(restaurant.ImageUrl);
        dto.IsFavorite.Should().BeTrue();
    }

    [Fact]
    public void ToCardDto_NullCity_CityNameIsNull()
    {
        var restaurant = new RestaurantBuilder().Build();
        restaurant.City = null;

        var dto = restaurant.ToCardDto(false);

        dto.CityName.Should().BeNull();
    }

    [Fact]
    public void ToCardDto_NullIsFavorite_DefaultsFalse()
    {
        var restaurant = new RestaurantBuilder().Build();

        var dto = restaurant.ToCardDto(null);

        dto.IsFavorite.Should().BeFalse();
    }

    [Fact]
    public void ToDetailDto_AllFields_MapsCorrectly()
    {
        var city = new City { CityId = 1, CityName = "Krakow" };
        var hours = new List<RestaurantOpeningHours>
        {
            new() { DayOfWeek = 1, OpenTime = new TimeOnly(9, 0), CloseTime = new TimeOnly(22, 0), IsClosed = false }
        };
        var sections = new List<MenuSection>
        {
            new() { SectionName = "Pasta", DisplayOrder = 2 },
            new() { SectionName = "Appetizers", DisplayOrder = 1 }
        };
        var restaurant = new RestaurantBuilder()
            .WithCity(city)
            .WithOpeningHours(hours)
            .WithMenuSections(sections)
            .Build();

        var dto = restaurant.ToDetailDto(true, 15);

        dto.PublicId.Should().Be(restaurant.PublicId);
        dto.CityName.Should().Be("Krakow");
        dto.AvgService.Should().Be(restaurant.AvgService);
        dto.AvgCleanliness.Should().Be(restaurant.AvgCleanliness);
        dto.AvgAmbiance.Should().Be(restaurant.AvgAmbiance);
        dto.Description.Should().Be(restaurant.Description);
        dto.Address.Should().Be(restaurant.Address);
        dto.IsVerified.Should().Be(restaurant.IsVerified);
        dto.IsFavorite.Should().BeTrue();
        dto.ReviewCount.Should().Be(15);
        dto.OpeningHours.Should().HaveCount(1);
        dto.OpeningHours[0].DayOfWeek.Should().Be(1);
        dto.MenuSections.Should().HaveCount(2);
        dto.MenuSections[0].SectionName.Should().Be("Appetizers"); // ordered by DisplayOrder
        dto.MenuSections[1].SectionName.Should().Be("Pasta");
    }

    [Fact]
    public void ToDetailDto_NullCollections_ReturnsEmptyLists()
    {
        var restaurant = new RestaurantBuilder().Build();
        restaurant.OpeningHours = null!;
        restaurant.MenuSections = null!;

        var dto = restaurant.ToDetailDto(false);

        dto.OpeningHours.Should().BeEmpty();
        dto.MenuSections.Should().BeEmpty();
    }

    [Fact]
    public void ToDetailDto_NullSlug_ReturnsEmptyString()
    {
        var restaurant = new RestaurantBuilder().WithSlug(null!).Build();
        restaurant.Slug = null;

        var dto = restaurant.ToDetailDto(false);

        dto.Slug.Should().BeEmpty();
    }
}
