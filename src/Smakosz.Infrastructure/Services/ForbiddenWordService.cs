using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Smakosz.Application.Common.Interfaces;
using Smakosz.Domain.Entities.System;
using Smakosz.Domain.Enums;

namespace Smakosz.Infrastructure.Services;

public class ForbiddenWordService : IForbiddenWordService
{
    private const string CacheKey = "forbidden_words";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private readonly ISmakoszDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ForbiddenWordService> _logger;

    public ForbiddenWordService(ISmakoszDbContext db, IMemoryCache cache, ILogger<ForbiddenWordService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> ContainsAsync(string text, CancellationToken ct, params ForbiddenWordCategory[] categories)
    {
        if (string.IsNullOrWhiteSpace(text) || categories.Length == 0)
            return false;

        var words = await GetCachedWordsAsync(ct);

        var filtered = words.Where(w => categories.Contains(w.Category)).ToList();
        var textLower = text.ToLowerInvariant();

        foreach (var word in filtered.Where(w => !w.IsRegex))
        {
            if (textLower.Contains(word.Word.ToLowerInvariant(), StringComparison.Ordinal))
                return true;
        }

        foreach (var word in filtered.Where(w => w.IsRegex))
        {
            try
            {
                if (Regex.IsMatch(text, word.Word, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1)))
                    return true;
            }
            catch (RegexParseException ex)
            {
                _logger.LogWarning(ex, "Invalid regex pattern in forbidden word #{WordId}: {Pattern}", word.WordId, word.Word);
            }
        }

        return false;
    }

    private async Task<List<ForbiddenWord>> GetCachedWordsAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out List<ForbiddenWord>? cached) && cached is not null)
            return cached;

        var words = await _db.ForbiddenWords.AsNoTracking().ToListAsync(ct);

        _cache.Set(CacheKey, words, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        });

        return words;
    }
}
