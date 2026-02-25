using FluentAssertions;
using Smakosz.Client.Helpers;

namespace Smakosz.ClientTests;

public class ImageHelperTests
{
    [Fact]
    public void GetImageUrl_WithValidUrl_ReturnsUrl()
    {
        var result = ImageHelper.GetImageUrl("https://example.com/photo.jpg");
        result.Should().Be("https://example.com/photo.jpg");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetImageUrl_WithNullOrEmpty_ReturnsFallbackOrDefault(string? imageUrl)
    {
        var result = ImageHelper.GetImageUrl(imageUrl);
        result.Should().Contain("unsplash.com");
    }

    [Fact]
    public void GetImageUrl_WithNullAndCustomFallback_ReturnsFallback()
    {
        var result = ImageHelper.GetImageUrl(null, "https://fallback.com/img.jpg");
        result.Should().Be("https://fallback.com/img.jpg");
    }

    [Fact]
    public void GetDishImage_WithValidUrl_ReturnsUrl()
    {
        var result = ImageHelper.GetDishImage("https://example.com/dish.jpg");
        result.Should().Be("https://example.com/dish.jpg");
    }

    [Fact]
    public void GetDishImage_WithNull_ReturnsDishPlaceholder()
    {
        var result = ImageHelper.GetDishImage(null);
        result.Should().Contain("unsplash.com").And.Contain("photo-1546069901");
    }

    [Fact]
    public void GetRestaurantImage_WithValidUrl_ReturnsUrl()
    {
        var result = ImageHelper.GetRestaurantImage("https://example.com/rest.jpg");
        result.Should().Be("https://example.com/rest.jpg");
    }

    [Fact]
    public void GetRestaurantImage_WithNull_ReturnsRestaurantPlaceholder()
    {
        var result = ImageHelper.GetRestaurantImage(null);
        result.Should().Contain("unsplash.com").And.Contain("photo-1517248135467");
    }

    [Fact]
    public void GetAvatarUrl_WithValidUrl_ReturnsUrl()
    {
        var result = ImageHelper.GetAvatarUrl("https://example.com/avatar.jpg");
        result.Should().Be("https://example.com/avatar.jpg");
    }

    [Fact]
    public void GetAvatarUrl_WithNull_ReturnsUiAvatarsUrl()
    {
        var result = ImageHelper.GetAvatarUrl(null, "Jan");
        result.Should().Contain("ui-avatars.com")
            .And.Contain("name=Jan")
            .And.Contain("D4A574")
            .And.Contain("4A3428");
    }

    [Fact]
    public void GetAvatarUrl_WithNullAndNoUsername_UsesDefaultName()
    {
        var result = ImageHelper.GetAvatarUrl(null);
        result.Should().Contain("name=User");
    }

    [Fact]
    public void GetAvatarUrl_EscapesSpecialCharacters()
    {
        var result = ImageHelper.GetAvatarUrl(null, "Jan Kowalski");
        result.Should().Contain("name=Jan%20Kowalski");
    }
}
