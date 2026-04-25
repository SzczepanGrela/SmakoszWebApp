using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

public class RestaurantBuilder
{
    private readonly Restaurant _restaurant = new()
    {
        RestaurantId = 1,
        PublicId = Guid.NewGuid(),
        RestaurantName = "Test Restaurant",
        Slug = "test-restaurant",
        Status = RestaurantStatus.Active,
        CuisineTypeId = 1,
        Cuisine = new CuisineType { CuisineTypeId = 1, Name = "Italian", DisplayName = "Italian" },
        PriceLevel = 2,
        AvgFoodScore = 7.5,
        AvgService = 7.0,
        AvgCleanliness = 8.0,
        AvgAmbiance = 7.5,
        TrendingScore = 50m,
        ImageUrl = "https://example.com/img.jpg",
        Description = "A test restaurant",
        Address = "123 Test St",
        Phone = "+48123456789",
        Email = "contact@test.com",
        Website = "https://test.com",
        IsVerified = true,
        CreatedAt = DateTime.UtcNow,
        OpeningHours = new List<RestaurantOpeningHours>(),
        MenuSections = new List<MenuSection>()
    };

    public RestaurantBuilder WithId(int id) { _restaurant.RestaurantId = id; return this; }
    public RestaurantBuilder WithPublicId(Guid id) { _restaurant.PublicId = id; return this; }
    public RestaurantBuilder WithName(string name) { _restaurant.RestaurantName = name; return this; }
    public RestaurantBuilder WithSlug(string slug) { _restaurant.Slug = slug; return this; }
    public RestaurantBuilder WithCuisineType(string cuisine)
    {
        _restaurant.Cuisine = new CuisineType { CuisineTypeId = 1, Name = cuisine, DisplayName = cuisine };
        _restaurant.CuisineTypeId = 1;
        return this;
    }

    public RestaurantBuilder WithCuisineTypeId(int? id) { _restaurant.CuisineTypeId = id; return this; }
    public RestaurantBuilder WithPriceLevel(int? level) { _restaurant.PriceLevel = level; return this; }
    public RestaurantBuilder WithCity(City city) { _restaurant.City = city; _restaurant.CityId = city.CityId; return this; }
    public RestaurantBuilder WithCityId(int? cityId) { _restaurant.CityId = cityId; return this; }
    public RestaurantBuilder WithTrendingScore(decimal? score) { _restaurant.TrendingScore = score; return this; }
    public RestaurantBuilder WithAvgFoodScore(double? score) { _restaurant.AvgFoodScore = score; return this; }
    public RestaurantBuilder WithStatus(RestaurantStatus status) { _restaurant.Status = status; return this; }
    public RestaurantBuilder AsActive() { _restaurant.Status = RestaurantStatus.Active; return this; }
    public RestaurantBuilder AsSuspended() { _restaurant.Status = RestaurantStatus.Suspended; return this; }
    public RestaurantBuilder WithOpeningHours(List<RestaurantOpeningHours> hours) { _restaurant.OpeningHours = hours; return this; }
    public RestaurantBuilder WithMenuSections(List<MenuSection> sections) { _restaurant.MenuSections = sections; return this; }

    public Restaurant Build() => _restaurant;
}
