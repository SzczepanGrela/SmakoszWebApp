using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Services;

public class StubNcfModelStorageService(ILogger<StubNcfModelStorageService> logger) : INcfModelStorageService
{
    public Task DownloadModelAsync(string version, string localBasePath, CancellationToken ct)
    {
        logger.LogWarning("INcfModelStorageService not configured (R2Models:AccountId empty). Skipping download of version {Version}", version);
        return Task.CompletedTask;
    }

    public Task<string> UploadTrainingDataAsync(Stream data, string key, CancellationToken ct)
    {
        logger.LogWarning("INcfModelStorageService not configured (R2Models:AccountId empty). Skipping upload of {Key}", key);
        return Task.FromResult($"stub://{key}");
    }

    public Task CleanupOldFilesAsync(string prefix, int keepCount, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
