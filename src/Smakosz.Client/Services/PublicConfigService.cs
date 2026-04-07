using System.Net.Http.Json;

namespace Smakosz.Client.Services;

public class PublicConfigService : IPublicConfigService
{
    private readonly HttpClient _http;
    private Dictionary<string, string>? _cache;

    public PublicConfigService(HttpClient http)
    {
        _http = http;
    }

    public async Task<Dictionary<string, string>> GetConfigAsync()
    {
        if (_cache is not null)
            return _cache;

        try
        {
            _cache = await _http.GetFromJsonAsync<Dictionary<string, string>>("api/config/public")
                     ?? new Dictionary<string, string>();
        }
        catch
        {
            _cache = new Dictionary<string, string>();
        }

        return _cache;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue)
    {
        var config = await GetConfigAsync();
        return config.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue)
    {
        var config = await GetConfigAsync();
        return config.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : defaultValue;
    }

    public async Task<string?> GetValueAsync(string key)
    {
        var config = await GetConfigAsync();
        return config.TryGetValue(key, out var value) ? value : null;
    }
}
