using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Orchestrator.Services;

public class StubNcfModelStorageService(ILogger<StubNcfModelStorageService> logger) : INcfModelStorageService
{
    public Task DownloadModelAsync(string version, string localBasePath, CancellationToken ct)
    {
        logger.LogWarning("INcfModelStorageService not configured (R2Models:AccountId empty). Skipping download of version {Version}", version);
        return Task.CompletedTask;
    }
}
