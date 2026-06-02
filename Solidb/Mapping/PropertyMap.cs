using System;
using System.Reflection;

namespace Solidb.Mapping
{
    public sealed class PropertyMap
    {
        public string PropertyName { get; }
        public string ColumnName { get; }
        public Type PropertyType { get; }
        public PropertyInfo PropertyInfo { get; }
        public bool IsKey { get; }
        public bool IsGenerated { get; }

        public PropertyMap(
            string propertyName,
            string columnName,
            Type propertyType,
            PropertyInfo propertyInfo,
            bool isKey = false,
            bool isGenerated = false)
        {
            PropertyName = propertyName;
            ColumnName = columnName;
            PropertyType = propertyType;
            PropertyInfo = propertyInfo;
            IsKey = isKey;
            IsGenerated = isGenerated;
        }
    }
}
