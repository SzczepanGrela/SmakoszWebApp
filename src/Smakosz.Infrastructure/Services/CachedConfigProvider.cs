using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class CachedConfigProvider : IPublicConfigProvider, IValidationConfigProvider
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;

    private const string AllConfigsCacheKey = "SystemConfigs:All";
    private const string PublicConfigsCacheKey = "SystemConfigs:Public";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    public CachedConfigProvider(IServiceScopeFactory scopeFactory, IMemoryCache cache)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
    }

    public async Task<Dictionary<string, string>> GetPublicConfigAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(PublicConfigsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
            return await db.SystemConfigs
                .Where(c => c.IsPublic)
                .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
        }) ?? new Dictionary<string, string>();
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        var all = await GetAllConfigsAsync(ct);
        return all.TryGetValue(key, out var value) ? value : null;
    }

    public async Task<int> GetIntAsync(string key, int defaultValue, CancellationToken ct)
    {
        var raw = await GetValueAsync(key, ct);
        return raw is not null && int.TryParse(raw, out var value) ? value : defaultValue;
    }

    public async Task<double> GetDoubleAsync(string key, double defaultValue, CancellationToken ct)
    {
        var raw = await GetValueAsync(key, ct);
        return raw is not null
               && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : defaultValue;
    }

    public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken ct)
    {
        var raw = await GetValueAsync(key, ct);
        return raw is not null && bool.TryParse(raw, out var value) ? value : defaultValue;
    }

    public void InvalidateCache()
    {
        _cache.Remove(AllConfigsCacheKey);
        _cache.Remove(PublicConfigsCacheKey);
    }
    public int GetInt(string key, int defaultValue)
    {
        var all = _cache.GetOrCreate(AllConfigsCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
            return db.SystemConfigs.ToDictionary(c => c.Key, c => c.Value);
        }) ?? new Dictionary<string, string>();

        return all.TryGetValue(key, out var raw) && int.TryParse(raw, out var value) ? value : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue)
    {
        var all = _cache.GetOrCreate(AllConfigsCacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
            return db.SystemConfigs.ToDictionary(c => c.Key, c => c.Value);
        }) ?? new Dictionary<string, string>();

        return all.TryGetValue(key, out var raw) && bool.TryParse(raw, out var value) ? value : defaultValue;
    }

    private async Task<Dictionary<string, string>> GetAllConfigsAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync(AllConfigsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
            return await db.SystemConfigs
                .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
        }) ?? new Dictionary<string, string>();
    }
}
