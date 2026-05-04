using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Smakosz.Application.Common.Interfaces;

namespace Smakosz.Infrastructure.Services;

public class DirectConfigProvider : IPublicConfigProvider, IValidationConfigProvider
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DirectConfigProvider(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<Dictionary<string, string>> GetPublicConfigAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
        return await db.SystemConfigs
            .Where(c => c.IsPublic)
            .ToDictionaryAsync(c => c.Key, c => c.Value, ct);
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
        var entry = await db.SystemConfigs
            .Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefaultAsync(ct);
        return entry;
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

    public void InvalidateCache() { }

    public int GetInt(string key, int defaultValue)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
        var raw = db.SystemConfigs
            .Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefault();
        return raw is not null && int.TryParse(raw, out var value) ? value : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ISmakoszDbContext>();
        var raw = db.SystemConfigs
            .Where(c => c.Key == key)
            .Select(c => c.Value)
            .FirstOrDefault();
        return raw is not null && bool.TryParse(raw, out var value) ? value : defaultValue;
    }
}
