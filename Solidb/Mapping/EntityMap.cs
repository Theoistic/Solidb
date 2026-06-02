using System;
using System.Collections.Generic;

namespace Solidb.Mapping
{
    public sealed class EntityMap
    {
        public Type ClrType { get; }
        public string TableName { get; }
        public PropertyMap Key { get; }
        public IReadOnlyList<PropertyMap> Properties { get; }
        public IReadOnlyList<RelationMap> Relations { get; }

        public EntityMap(
            Type clrType,
            string tableName,
            PropertyMap key,
            IReadOnlyList<PropertyMap> properties,
            IReadOnlyList<RelationMap> relations)
        {
            ClrType = clrType;
            TableName = tableName;
            Key = key;
            Properties = properties;
            Relations = relations;
        }
    }
}
