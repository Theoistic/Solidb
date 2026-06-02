using System.Collections.Generic;

namespace Solidb.Tracking
{
    public sealed class TrackedEntity
    {
        public object Entity { get; }
        public EntityState State { get; set; }
        public IReadOnlyDictionary<string, object?> OriginalValues { get; }

        public TrackedEntity(object entity, EntityState state, Dictionary<string, object?> originalValues)
        {
            Entity = entity;
            State = state;
            OriginalValues = originalValues;
        }
    }
}
