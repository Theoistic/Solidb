using System;
using System.Collections.Generic;

namespace Solidb.Mapping
{
    public sealed class SolidModel
    {
        private readonly Dictionary<Type, EntityMap> _maps = new();

        public void Register(EntityMap map) => _maps[map.ClrType] = map;

        public EntityMap GetEntity(Type type)
        {
            if (_maps.TryGetValue(type, out var map))
                return map;
            throw new InvalidOperationException(
                $"No entity map found for '{type.Name}'. Ensure it was passed to the SolidContext constructor.");
        }

        public EntityMap GetEntity<T>() => GetEntity(typeof(T));

        public bool HasEntity(Type type) => _maps.ContainsKey(type);

        public IEnumerable<EntityMap> All() => _maps.Values;
    }
}
