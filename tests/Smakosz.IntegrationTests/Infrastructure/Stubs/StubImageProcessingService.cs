using Smakosz.Application.Common.Interfaces;

namespace Smakosz.IntegrationTests.Infrastructure.Stubs;

public class StubImageProcessingService : IImageProcessingService
{
    public Task<(MemoryStream Stream, int Width, int Height)> ResizeToWebpAsync(Stream input, int maxWidth)
    {
        var ms = new MemoryStream([0x00]);
        return Task.FromResult((ms, 100, 100));
    }

    public Task<string> GenerateBlurhashAsync(Stream input)
    {
        return Task.FromResult("LEHV6nWB2yk8pyo0adR*.7kCMdnj");
    }
}
