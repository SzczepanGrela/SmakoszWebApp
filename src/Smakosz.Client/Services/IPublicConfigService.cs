namespace Smakosz.Client.Services;

public interface IPublicConfigService
{
    Task<Dictionary<string, string>> GetConfigAsync();
    Task<int> GetIntAsync(string key, int defaultValue);
    Task<bool> GetBoolAsync(string key, bool defaultValue);
    Task<string?> GetValueAsync(string key);
}
