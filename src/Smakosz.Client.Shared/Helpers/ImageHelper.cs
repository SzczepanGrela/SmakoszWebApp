namespace Smakosz.Client.Helpers;

public static class ImageHelper
{
    private const string DefaultDishPlaceholder = "/images/dish-placeholder.svg";
    private const string DefaultRestaurantPlaceholder = "/images/restaurant-placeholder.svg";

    public static string GetImageUrl(string? imageUrl, string? fallback = null)
        => !string.IsNullOrWhiteSpace(imageUrl) ? imageUrl : fallback ?? DefaultDishPlaceholder;

    public static string GetDishImage(string? imageUrl)
        => GetImageUrl(imageUrl, DefaultDishPlaceholder);

    public static string GetRestaurantImage(string? imageUrl)
        => GetImageUrl(imageUrl, DefaultRestaurantPlaceholder);

    public static string GetAvatarUrl(string? avatarUrl, string? username = null)
    {
        if (!string.IsNullOrWhiteSpace(avatarUrl))
            return avatarUrl;

        var safeName = string.IsNullOrWhiteSpace(username) ? "US" : username.Trim();
        var encoded = Uri.EscapeDataString(safeName);
        return $"https://ui-avatars.com/api/?name={encoded}&background=D4A574&color=4A3428&size=128";
    }
}
