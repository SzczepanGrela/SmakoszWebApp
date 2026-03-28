namespace Smakosz.Application.Common.Interfaces;

public interface INcfModelStorageService
{
    Task DownloadModelAsync(string version, string localBasePath, CancellationToken ct);
}
