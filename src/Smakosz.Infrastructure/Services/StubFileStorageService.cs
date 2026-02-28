using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class StubFileStorageService : IFileStorageService
{
    private readonly ILogger<StubFileStorageService> _logger;

    public StubFileStorageService(ILogger<StubFileStorageService> logger) => _logger = logger;

    public Task<FileUploadResult> UploadAsync(Stream file, string fileName, string folder, CancellationToken ct = default)
    {
        var key = $"{folder}/{fileName}";
        _logger.LogInformation("[Storage Stub] Uploaded {Key} ({Bytes} bytes)", key, file.Length);
        var url = $"https://assets.smakosz.xyz/{key}";
        return Task.FromResult(new FileUploadResult(key, url, null, null, null, null));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _logger.LogInformation("[Storage Stub] Deleted {Key}", key);
        return Task.CompletedTask;
    }

    public Task<string> UploadRawAsync(Stream data, string key, string contentType, CancellationToken ct = default)
    {
        _logger.LogInformation("[Storage Stub] Raw upload {Key} ({ContentType})", key, contentType);
        return Task.FromResult($"https://stub-cdn.local/{key}");
    }

    public string GetPublicUrl(string key) => $"https://assets.smakosz.xyz/{key}";
}
