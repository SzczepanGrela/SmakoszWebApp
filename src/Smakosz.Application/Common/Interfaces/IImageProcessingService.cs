namespace Smakosz.Application.Common.Interfaces;

public interface IImageProcessingService
{
    Task<(MemoryStream Stream, int Width, int Height)> ResizeToWebpAsync(Stream input, int maxWidth);
    Task<string> GenerateBlurhashAsync(Stream input);
}
