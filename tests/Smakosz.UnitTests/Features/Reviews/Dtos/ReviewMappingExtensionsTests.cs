using FluentAssertions;
using Smakosz.Application.Features.Reviews.Dtos;
using Smakosz.Domain.Enums;
using Smakosz.UnitTests.Common.TestInfrastructure.EntityBuilders;

namespace Smakosz.UnitTests.Features.Reviews.Dtos;

[Trait("Category", "Mapping")]
public class ReviewMappingExtensionsTests
{
    [Fact]
    public void ToCardDto_AllFields_MapsCorrectly()
    {
        var user = new UserBuilder()
            .WithUsername("reviewer1")
            .WithSlug("reviewer1")
            .WithAvatarUrl("https://example.com/avatar.jpg")
            .WithReviewCount(10)
            .Build();
        var restaurant = new RestaurantBuilder()
            .WithName("Bella Italia")
            .WithSlug("bella-italia")
            .Build();
        var dish = new DishBuilder()
            .WithName("Margherita")
            .WithSlug("margherita")
            .Build();
        var review = new ReviewBuilder()
            .WithUser(user)
            .WithRestaurant(restaurant)
            .WithDish(dish)
            .WithDishRating(9)
            .WithContent("Excellent pizza!")
            .WithContentStatus(ReviewContentStatus.Approved)
            .WithHelpfulCount(5)
            .Build();

        var dto = review.ToCardDto(true);

        dto.PublicId.Should().Be(review.PublicId);
        dto.DishRating.Should().Be(9);
        dto.ServiceRating.Should().Be(review.ServiceRating);
        dto.CleanlinessRating.Should().Be(review.CleanlinessRating);
        dto.AmbianceRating.Should().Be(review.AmbianceRating);
        dto.Content.Should().Be("Excellent pizza!");
        dto.ContentStatus.Should().Be(ReviewContentStatus.Approved);
        dto.VisitDate.Should().Be(review.VisitDate);
        dto.HelpfulCount.Should().Be(5);
        dto.IsHelpfulByMe.Should().BeTrue();
        dto.CreatedAt.Should().Be(review.CreatedAt);
        dto.UpdatedAt.Should().Be(review.UpdatedAt);
    }

    [Fact]
    public void ToCardDto_AuthorFields_MapsCorrectly()
    {
        var user = new UserBuilder()
            .WithUsername("reviewer1")
            .WithSlug("reviewer1")
            .WithAvatarUrl("https://example.com/avatar.jpg")
            .WithReviewCount(10)
            .Build();
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().Build();
        var review = new ReviewBuilder()
            .WithUser(user)
            .WithRestaurant(restaurant)
            .WithDish(dish)
            .Build();

        var dto = review.ToCardDto(false);

        dto.Author.PublicId.Should().Be(user.PublicId);
        dto.Author.Slug.Should().Be("reviewer1");
        dto.Author.Username.Should().Be("reviewer1");
        dto.Author.AvatarUrl.Should().Be("https://example.com/avatar.jpg");
        dto.Author.ReviewCount.Should().Be(10);
    }

    [Fact]
    public void ToCardDto_IsHelpfulByMeFalse_MapsCorrectly()
    {
        var user = new UserBuilder().Build();
        var restaurant = new RestaurantBuilder().Build();
        var dish = new DishBuilder().Build();
        var review = new ReviewBuilder()
            .WithUser(user)
            .WithRestaurant(restaurant)
            .WithDish(dish)
            .Build();

        var dto = review.ToCardDto(false);

        dto.IsHelpfulByMe.Should().BeFalse();
    }

    [Fact]
    public void ToCardDto_DishAndRestaurantNames_MapsCorrectly()
    {
        var user = new UserBuilder().Build();
        var restaurant = new RestaurantBuilder()
            .WithName("Pasta House")
            .WithSlug("pasta-house")
            .Build();
        var dish = new DishBuilder()
            .WithName("Carbonara")
            .WithSlug("carbonara")
            .Build();
        var review = new ReviewBuilder()
            .WithUser(user)
            .WithRestaurant(restaurant)
            .WithDish(dish)
            .Build();

        var dto = review.ToCardDto(false);

        dto.DishName.Should().Be("Carbonara");
        dto.DishSlug.Should().Be("carbonara");
        dto.RestaurantName.Should().Be("Pasta House");
        dto.RestaurantSlug.Should().Be("pasta-house");
    }
}
