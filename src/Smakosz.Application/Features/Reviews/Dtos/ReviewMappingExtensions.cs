using Smakosz.Domain.Entities;

namespace Smakosz.Application.Features.Reviews.Dtos;

public static class ReviewMappingExtensions
{
    public static ReviewCardDto ToCardDto(this Review r, bool isHelpfulByMe)
    {
        return new ReviewCardDto
        {
            PublicId = r.PublicId,
            DishRating = r.DishRating,
            ServiceRating = r.ServiceRating,
            CleanlinessRating = r.CleanlinessRating,
            AmbianceRating = r.AmbianceRating,
            Content = r.Content,
            ContentStatus = r.ModerationStatus,
            VisitDate = r.VisitDate,
            HelpfulCount = r.HelpfulCount,
            IsHelpfulByMe = isHelpfulByMe,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            Author = new UserSummaryDto
            {
                PublicId = r.User.PublicId,
                Slug = r.User.Slug ?? string.Empty,
                Username = r.User.Username,
                AvatarUrl = r.User.AvatarUrl,
                AvatarBlurhash = r.User.AvatarBlurhash,
                ReviewCount = r.User.ReviewCount
            },
            DishName = r.Dish.DishName,
            DishSlug = r.Dish.Slug ?? string.Empty,
            RestaurantName = r.Restaurant.RestaurantName,
            RestaurantSlug = r.Restaurant.Slug ?? string.Empty
        };
    }
}
