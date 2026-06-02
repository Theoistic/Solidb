using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Solidb.Mapping;

namespace Solidb.Materialization
{
    public static class RelationFixer
    {
        /// <summary>Attach one-to-many children to their parents via the FK property on the child.</summary>
        public static void FixOneToMany(
            IList parents,
            IList children,
            RelationMap relation)
        {
            var parentKeyProp = relation.PrincipalType
                .GetProperties()
                .FirstOrDefault(p =>
                    p.GetCustomAttributes(typeof(Mapping.KeyAttribute), false).Length > 0
                    || string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase));
            if (parentKeyProp == null) return;

            var childFkProp = relation.DependentType.GetProperty(relation.ForeignKeyName);
            if (childFkProp == null) return;

            var navProp = relation.PrincipalType.GetProperty(relation.NavigationName);
            if (navProp == null) return;

            // Build parent lookup keyed by string representation of PK
            var parentLookup = new Dictionary<string, object>();
            foreach (var parent in parents)
            {
                var key = parentKeyProp.GetValue(parent)?.ToString();
                if (key != null) parentLookup[key] = parent;
            }

            var listType = typeof(List<>).MakeGenericType(relation.DependentType);

            // Ensure each parent has an initialised collection
            foreach (var parent in parents)
            {
                if (navProp.GetValue(parent) == null)
                    navProp.SetValue(parent, Activator.CreateInstance(listType));
            }

            foreach (var child in children)
            {
                var fkValue = childFkProp.GetValue(child)?.ToString();
                if (fkValue == null) continue;
                if (!parentLookup.TryGetValue(fkValue, out var parent)) continue;

                var collection = (IList)navProp.GetValue(parent)!;
                collection.Add(child);
            }
        }

        /// <summary>Set many-to-one navigation property on each child using the FK value.</summary>
        public static void FixManyToOne(
            IList children,
            IList parents,
            RelationMap relation)
        {
            var parentKeyProp = relation.DependentType
                .GetProperties()
                .FirstOrDefault(p =>
                    p.GetCustomAttributes(typeof(Mapping.KeyAttribute), false).Length > 0
                    || string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase));
            if (parentKeyProp == null) return;

            var childFkProp = relation.PrincipalType.GetProperty(relation.ForeignKeyName);
            if (childFkProp == null) return;

            var navProp = relation.PrincipalType.GetProperty(relation.NavigationName);
            if (navProp == null) return;

            var parentLookup = new Dictionary<string, object>();
            foreach (var parent in parents)
            {
                var key = parentKeyProp.GetValue(parent)?.ToString();
                if (key != null) parentLookup[key] = parent;
            }

            foreach (var child in children)
            {
                var fkValue = childFkProp.GetValue(child)?.ToString();
                if (fkValue != null && parentLookup.TryGetValue(fkValue, out var parent))
                    navProp.SetValue(child, parent);
            }
        }
    }
}
