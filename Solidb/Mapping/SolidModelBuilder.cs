using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Solidb.Mapping
{
    public static class SolidModelBuilder
    {
        public static SolidModel Build(params Type[] entityTypes)
        {
            var model = new SolidModel();
            foreach (var type in entityTypes)
                model.Register(BuildEntityMap(type));
            return model;
        }

        public static EntityMap BuildEntityMap(Type type)
        {
            var tableName = type.GetCustomAttribute<TableAttribute>()?.Name
                ?? type.Name.ToLowerInvariant() + "s";

            var allProps = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite
                    && p.GetCustomAttribute<NotMappedAttribute>() == null)
                .ToList();

            var scalarProps = allProps.Where(p => !IsNavigationProperty(p)).ToList();
            var navigationProps = allProps.Where(IsNavigationProperty).ToList();

            PropertyMap? keyMap = null;
            var properties = new List<PropertyMap>();

            foreach (var prop in scalarProps)
            {
                var colName = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;
                var isKey = prop.GetCustomAttribute<KeyAttribute>() != null
                    || string.Equals(prop.Name, "Id", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prop.Name, type.Name + "Id", StringComparison.OrdinalIgnoreCase);
                var isGenerated = prop.GetCustomAttribute<GeneratedAttribute>() != null;

                var pm = new PropertyMap(prop.Name, colName, prop.PropertyType, prop, isKey, isGenerated);
                properties.Add(pm);
                if (isKey && keyMap == null)
                    keyMap = pm;
            }

            if (keyMap == null)
                throw new InvalidOperationException(
                    $"Entity '{type.Name}' has no key property. Add a [Key] attribute or a property named 'Id' or '{type.Name}Id'.");

            var relations = new List<RelationMap>();
            foreach (var nav in navigationProps)
            {
                var relation = BuildRelationMap(type, nav, properties);
                if (relation != null)
                    relations.Add(relation);
            }

            return new EntityMap(type, tableName, keyMap, properties, relations);
        }

        private static bool IsNavigationProperty(PropertyInfo prop)
        {
            var t = prop.PropertyType;
            if (t == typeof(string)) return false;
            if (t.IsPrimitive || t.IsValueType) return false;
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) return false;
            if (typeof(IEnumerable).IsAssignableFrom(t)) return true;
            return t.IsClass;
        }

        private static RelationMap? BuildRelationMap(Type ownerType, PropertyInfo nav, List<PropertyMap> ownerProps)
        {
            var navType = nav.PropertyType;
            Type? dependentType;
            RelationKind kind;

            if (typeof(IEnumerable).IsAssignableFrom(navType) && navType != typeof(string))
            {
                var elementType = navType.IsArray
                    ? navType.GetElementType()
                    : navType.GenericTypeArguments.FirstOrDefault();
                if (elementType == null) return null;
                dependentType = elementType;
                kind = RelationKind.OneToMany;
            }
            else
            {
                dependentType = navType;
                kind = RelationKind.ManyToOne;
            }

            var fkAttr = nav.GetCustomAttribute<ForeignKeyAttribute>();
            string? fkName = fkAttr?.Name;

            if (fkName == null)
            {
                var candidate1 = nav.Name + "Id";
                var candidate2 = dependentType.Name + "Id";
                fkName = ownerProps
                    .FirstOrDefault(p => string.Equals(p.PropertyName, candidate1, StringComparison.OrdinalIgnoreCase))
                    ?.PropertyName
                    ?? ownerProps
                    .FirstOrDefault(p => string.Equals(p.PropertyName, candidate2, StringComparison.OrdinalIgnoreCase))
                    ?.PropertyName;
            }

            if (fkName == null && kind == RelationKind.OneToMany)
                fkName = ownerType.Name + "Id";

            fkName ??= dependentType.Name + "Id";

            return new RelationMap(nav.Name, ownerType, dependentType, fkName, kind);
        }
    }
}
