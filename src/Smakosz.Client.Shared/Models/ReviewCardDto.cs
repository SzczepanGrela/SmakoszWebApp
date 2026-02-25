namespace Smakosz.Client.Models;

public class ReviewCardDto
{
    public Guid PublicId { get; set; }
    public int DishRating { get; set; }
    public int ServiceRating { get; set; }
    public int CleanlinessRating { get; set; }
    public int AmbianceRating { get; set; }
    public string? Content { get; set; }
    public string ContentStatus { get; set; } = default!;
    public string VisitDate { get; set; } = default!;
    public int HelpfulCount { get; set; }
    public bool IsHelpfulByMe { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public UserSummaryDto Author { get; set; } = new();
    public string DishName { get; set; } = default!;
    public string DishSlug { get; set; } = default!;
    public string RestaurantName { get; set; } = default!;
    public string RestaurantSlug { get; set; } = default!;
}

public class UserSummaryDto
{
    public Guid PublicId { get; set; }
    public string Slug { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? AvatarBlurhash { get; set; }
    public int ReviewCount { get; set; }
}
