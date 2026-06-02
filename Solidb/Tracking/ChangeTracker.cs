using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Solidb.Tracking
{
    public sealed class ChangeTracker
    {
        private readonly Dictionary<object, TrackedEntity> _tracked =
            new(ReferenceEqualityComparer.Instance);

        public void Track(object entity, EntityState state = EntityState.Unchanged)
        {
            if (_tracked.TryGetValue(entity, out var existing))
            {
                if (existing.State == EntityState.Unchanged && state != EntityState.Unchanged)
                    existing.State = state;
                return;
            }
            _tracked[entity] = new TrackedEntity(entity, state, Snapshot(entity));
        }

        public bool IsTracked(object entity) => _tracked.ContainsKey(entity);

        public IReadOnlyList<TrackedEntity> Entries() => _tracked.Values.ToList();

        public void DetectChanges()
        {
            foreach (var entry in _tracked.Values.Where(e => e.State == EntityState.Unchanged))
            {
                var current = Snapshot(entry.Entity);
                foreach (var kv in current)
                {
                    if (!Equals(kv.Value, entry.OriginalValues.GetValueOrDefault(kv.Key)))
                    {
                        entry.State = EntityState.Modified;
                        break;
                    }
                }
            }
        }

        public void AcceptAllChanges()
        {
            foreach (var entry in _tracked.Values.ToList())
            {
                if (entry.State == EntityState.Deleted)
                    _tracked.Remove(entry.Entity);
                else
                    entry.State = EntityState.Unchanged;
            }
        }

        public void Clear() => _tracked.Clear();

        private static Dictionary<string, object?> Snapshot(object entity) =>
            entity.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .ToDictionary(p => p.Name, p => p.GetValue(entity));
    }
}
