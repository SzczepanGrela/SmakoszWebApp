using ErrorOr;
using Smakosz.Application.Common.Errors;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Application.Common.Helpers;

public static class ImageDimensionValidator
{
    public const double DefaultTolerance = 0.02;

    public static async Task<ErrorOr<Success>> ValidateRatioAsync(
        Stream image,
        IImageProcessingService imageProcessor,
        double targetRatio,
        double tolerance = DefaultTolerance)
    {
        var initialPosition = image.CanSeek ? image.Position : 0;
        var dims = await imageProcessor.IdentifyDimensionsAsync(image);
        if (image.CanSeek) image.Position = initialPosition;

        if (dims is null)
            return DomainErrors.Media.UnreadableImage;

        var actualRatio = (double)dims.Value.Width / dims.Value.Height;
        var diff = Math.Abs(actualRatio - targetRatio) / targetRatio;
        if (diff > tolerance)
            return DomainErrors.Media.WrongRatio;

        return Result.Success;
    }
}
