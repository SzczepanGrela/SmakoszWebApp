using Smakosz.Domain.Enums;

namespace Smakosz.Application.Features.Reviews.Dtos;

public class ReviewCardDto
{
    public Guid PublicId { get; init; }
    public int DishRating { get; init; }
    public int ServiceRating { get; init; }
    public int CleanlinessRating { get; init; }
    public int AmbianceRating { get; init; }
    public string? Content { get; init; }
    public ReviewContentStatus ContentStatus { get; init; }
    public DateOnly VisitDate { get; init; }
    public int HelpfulCount { get; init; }
    public bool IsHelpfulByMe { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required UserSummaryDto Author { get; init; }
    public string DishName { get; init; } = default!;
    public string DishSlug { get; init; } = default!;
    public string RestaurantName { get; init; } = default!;
    public string RestaurantSlug { get; init; } = default!;
}
