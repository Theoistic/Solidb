using System;

namespace Solidb.Caching
{
    public interface ISolidCache
    {
        bool TryGet<T>(string key, out T? value);
        void Set<T>(string key, T value, TimeSpan ttl);
        void Remove(string key);
        void Clear();
    }
}
