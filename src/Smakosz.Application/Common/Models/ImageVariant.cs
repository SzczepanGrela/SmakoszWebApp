using Smakosz.Domain.Enums;

namespace Smakosz.Application.Common.Models;

public record ImageVariant(string Suffix, int MaxWidth);

public static class ImageVariants
{
    public static readonly ImageVariant Full = new("", 1920);
    public static readonly ImageVariant Thumb = new("_thumb", 200);
    public static readonly ImageVariant Tiny = new("_tiny", 50);
    public static readonly ImageVariant Hero = new("_hero", 1200);

    public static IReadOnlyList<ImageVariant> ForEntityType(MediaEntityType type) => type switch
    {
        MediaEntityType.Dish => [Full, Thumb],
        MediaEntityType.Restaurant => [Full, Thumb, Hero],
        MediaEntityType.User => [Full, Tiny],
        MediaEntityType.Hero => [Full],
        MediaEntityType.Review => [Full, Thumb],
        _ => [Full, Thumb]
    };
}
