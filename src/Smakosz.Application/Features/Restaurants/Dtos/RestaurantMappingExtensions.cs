using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Restaurants.Dtos;

public static class RestaurantMappingExtensions
{
    public static RestaurantCardDto ToCardDto(this Restaurant r, bool? isFavorite, int reviewCount = 0)
    {
        return new RestaurantCardDto
        {
            PublicId = r.PublicId,
            Slug = r.Slug ?? string.Empty,
            RestaurantName = r.RestaurantName,
            CuisineType = r.CuisineType,
            CityName = r.City?.CityName,
            PriceLevel = r.PriceLevel,
            AvgFoodScore = r.AvgFoodScore,
            ReviewCount = reviewCount,
            ImageUrl = r.ImageUrl,
            ImageBlurhash = r.ImageBlurhash,
            IsFavorite = isFavorite ?? false
        };
    }

    public static RestaurantDetailDto ToDetailDto(this Restaurant r, bool isFavorite, int reviewCount = 0)
    {
        return new RestaurantDetailDto
        {
            PublicId = r.PublicId,
            Slug = r.Slug ?? string.Empty,
            RestaurantName = r.RestaurantName,
            CuisineType = r.CuisineType,
            CityName = r.City?.CityName,
            PriceLevel = r.PriceLevel,
            AvgFoodScore = r.AvgFoodScore,
            AvgService = r.AvgService,
            AvgCleanliness = r.AvgCleanliness,
            AvgAmbiance = r.AvgAmbiance,
            ReviewCount = reviewCount,
            TrendingScore = r.TrendingScore,
            ImageUrl = r.ImageUrl,
            ImageBlurhash = r.ImageBlurhash,
            Description = r.Description,
            Address = r.Address,
            Phone = r.Phone,
            Email = r.Email,
            Website = r.Website,
            Latitude = r.Latitude,
            Longitude = r.Longitude,
            IsVerified = r.IsVerified,
            IsFavorite = isFavorite,
            OpeningHours = r.OpeningHours?.Select(h => new OpeningHoursDto
            {
                DayOfWeek = h.DayOfWeek,
                OpenTime = h.OpenTime,
                CloseTime = h.CloseTime,
                IsClosed = h.IsClosed
            }).ToList() ?? [],
            MenuSections = r.MenuSections?.Select(s => new MenuSectionDto
            {
                SectionName = s.SectionName,
                DisplayOrder = s.DisplayOrder
            }).OrderBy(s => s.DisplayOrder).ToList() ?? []
        };
    }
}
