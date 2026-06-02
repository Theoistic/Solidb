using System;
using System.Collections.Generic;

namespace Solidb.Materialization
{
    public sealed class IdentityMap
    {
        private readonly Dictionary<(Type Type, string Key), object> _entities = new();

        public T GetOrAdd<T>(object key, Func<T> factory) where T : class
        {
            var mapKey = (typeof(T), key.ToString()!);
            if (_entities.TryGetValue(mapKey, out var existing))
                return (T)existing;
            var created = factory();
            _entities[mapKey] = created;
            return created;
        }

        public bool TryGet<T>(object key, out T? entity) where T : class
        {
            if (_entities.TryGetValue((typeof(T), key.ToString()!), out var value))
            {
                entity = (T)value;
                return true;
            }
            entity = null;
            return false;
        }

        public void Set<T>(object key, T entity) where T : class =>
            _entities[(typeof(T), key.ToString()!)] = entity;

        /// <summary>Non-generic version for use with runtime types.</summary>
        public object GetOrAdd(object key, Type type, Func<object> factory)
        {
            var mapKey = (type, key.ToString()!);
            if (_entities.TryGetValue(mapKey, out var existing))
                return existing;
            var created = factory();
            _entities[mapKey] = created;
            return created;
        }

        public bool TryGetByType(Type type, object key, out object? entity)
        {
            if (_entities.TryGetValue((type, key.ToString()!), out var value))
            {
                entity = value;
                return true;
            }
            entity = null;
            return false;
        }

        public void Clear() => _entities.Clear();
    }
}
