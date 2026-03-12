using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class ImageProcessingService : IImageProcessingService
{
    private static readonly WebpEncoder WebpEncoder = new() { Quality = 80 };

    public async Task<(MemoryStream Stream, int Width, int Height)> ResizeToWebpAsync(Stream input, int maxWidth)
    {
        using var image = await Image.LoadAsync(input);

        if (image.Width > maxWidth)
        {
            var ratio = (double)maxWidth / image.Width;
            var newHeight = (int)(image.Height * ratio);
            image.Mutate(x => x.Resize(maxWidth, newHeight));
        }

        var output = new MemoryStream();
        await image.SaveAsWebpAsync(output, WebpEncoder);
        output.Position = 0;

        return (output, image.Width, image.Height);
    }

    public async Task<string> GenerateBlurhashAsync(Stream input)
    {
        input.Position = 0;
        using var image = await Image.LoadAsync<SixLabors.ImageSharp.PixelFormats.Rgb24>(input);

        image.Mutate(x => x.Resize(32, 32));

        return Blurhash.ImageSharp.Blurhasher.Encode(image, 4, 3);
    }
}
