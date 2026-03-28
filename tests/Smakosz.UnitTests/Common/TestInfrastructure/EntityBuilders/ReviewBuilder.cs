using Smakosz.Domain.Entities;
using Smakosz.Domain.Enums;

namespace Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

public class ReviewBuilder
{
    private readonly Review _review = new()
    {
        ReviewId = 1,
        PublicId = Guid.NewGuid(),
        UserId = 1,
        DishId = 1,
        RestaurantId = 1,
        DishRating = 8,
        ServiceRating = 7,
        CleanlinessRating = 8,
        AmbianceRating = 7,
        Content = "Great food and service!",
        ModerationStatus = ContentModerationStatus.Approved,
        VisitDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
        IsVisible = true,
        IsApproved = true,
        IsDeleted = false,
        HelpfulCount = 3,
        CreatedAt = DateTime.UtcNow,
        User = null!,
        Dish = null!,
        Restaurant = null!
    };

    public ReviewBuilder WithId(int id) { _review.ReviewId = id; return this; }
    public ReviewBuilder WithPublicId(Guid id) { _review.PublicId = id; return this; }
    public ReviewBuilder WithUserId(int userId) { _review.UserId = userId; return this; }
    public ReviewBuilder WithDishId(int dishId) { _review.DishId = dishId; return this; }
    public ReviewBuilder WithRestaurantId(int restaurantId) { _review.RestaurantId = restaurantId; return this; }
    public ReviewBuilder WithUser(User user) { _review.User = user; _review.UserId = user.UserId; return this; }
    public ReviewBuilder WithDish(Dish dish) { _review.Dish = dish; _review.DishId = dish.DishId; return this; }
    public ReviewBuilder WithRestaurant(Restaurant restaurant) { _review.Restaurant = restaurant; _review.RestaurantId = restaurant.RestaurantId; return this; }
    public ReviewBuilder WithContent(string? content) { _review.Content = content; return this; }
    public ReviewBuilder WithContentStatus(ContentModerationStatus status) { _review.ModerationStatus = status; return this; }
    public ReviewBuilder WithDishRating(int rating) { _review.DishRating = rating; return this; }
    public ReviewBuilder WithHelpfulCount(int count) { _review.HelpfulCount = count; return this; }
    public ReviewBuilder WithCreatedAt(DateTime createdAt) { _review.CreatedAt = createdAt; return this; }
    public ReviewBuilder AsDeleted() { _review.IsDeleted = true; _review.DeletedAt = DateTime.UtcNow; return this; }
    public ReviewBuilder AsVisible() { _review.IsVisible = true; return this; }
    public ReviewBuilder WithIsApproved(bool? val) { _review.IsApproved = val; return this; }
    public ReviewBuilder AsHidden() { _review.IsVisible = false; return this; }

    public Review Build() => _review;
}
