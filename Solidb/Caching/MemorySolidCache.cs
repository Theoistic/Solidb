using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Solidb.Caching
{
    public sealed class MemorySolidCache : ISolidCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _store = new();

        public bool TryGet<T>(string key, out T? value)
        {
            if (_store.TryGetValue(key, out var entry) && !entry.IsExpired)
            {
                value = (T?)entry.Value;
                return true;
            }
            value = default;
            return false;
        }

        public void Set<T>(string key, T value, TimeSpan ttl) =>
            _store[key] = new CacheEntry(value, DateTimeOffset.UtcNow.Add(ttl));

        public void Remove(string key) => _store.TryRemove(key, out _);

        public void Clear() => _store.Clear();

        private sealed record CacheEntry(object? Value, DateTimeOffset ExpiresAt)
        {
            public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
        }
    }
}
