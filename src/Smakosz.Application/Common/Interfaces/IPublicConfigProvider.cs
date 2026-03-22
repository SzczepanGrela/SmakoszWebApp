namespace Smakosz.Application.Common.Interfaces;

public interface IPublicConfigProvider
{
    Task<Dictionary<string, string>> GetPublicConfigAsync(CancellationToken ct);
    Task<string?> GetValueAsync(string key, CancellationToken ct);
    Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct);
    Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct);
    void InvalidateCache();
}
