using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Smakosz.Infrastructure.Services;

public sealed class NullMemoryCache : IMemoryCache
{
    public bool TryGetValue(object key, out object? value)
    {
        value = null;
        return false;
    }

    public ICacheEntry CreateEntry(object key) => new NullCacheEntry(key);

    public void Remove(object key) { }

    public void Dispose() { }

    private sealed class NullCacheEntry : ICacheEntry
    {
        public NullCacheEntry(object key) { Key = key; }
        public object Key { get; }
        public object? Value { get; set; }
        public DateTimeOffset? AbsoluteExpiration { get; set; }
        public TimeSpan? AbsoluteExpirationRelativeToNow { get; set; }
        public TimeSpan? SlidingExpiration { get; set; }
        public IList<IChangeToken> ExpirationTokens { get; } = new List<IChangeToken>();
        public IList<PostEvictionCallbackRegistration> PostEvictionCallbacks { get; } = new List<PostEvictionCallbackRegistration>();
        public CacheItemPriority Priority { get; set; }
        public long? Size { get; set; }
        public void Dispose() { }
    }
}
