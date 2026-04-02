namespace Smakosz.Application.Common.Interfaces;

public interface INcfModelStorageService
{
    Task DownloadModelAsync(string version, string localBasePath, CancellationToken ct);
    Task<string> UploadTrainingDataAsync(Stream data, string key, CancellationToken ct);
    Task CleanupOldFilesAsync(string prefix, int keepCount, CancellationToken ct);
}
