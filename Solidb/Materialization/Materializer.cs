using System;
using System.Data;
using Solidb.Mapping;

namespace Solidb.Materialization
{
    public static class Materializer
    {
        public static T Materialize<T>(IDataRecord record, EntityMap map, string? prefix = null)
            where T : class, new()
        {
            var entity = new T();
            foreach (var prop in map.Properties)
            {
                var colName = prefix != null ? $"{prefix}_{prop.ColumnName}" : prop.ColumnName;
                int ordinal;
                try
                {
                    ordinal = record.GetOrdinal(colName);
                }
                catch (IndexOutOfRangeException)
                {
                    continue;
                }

                if (!record.IsDBNull(ordinal))
                    prop.PropertyInfo.SetValue(entity, ConvertValue(record.GetValue(ordinal), prop.PropertyType));
            }
            return entity;
        }

        public static object? ReadKey(IDataRecord record, EntityMap map, string? prefix = null)
        {
            var colName = prefix != null ? $"{prefix}_{map.Key.ColumnName}" : map.Key.ColumnName;
            int ordinal;
            try { ordinal = record.GetOrdinal(colName); }
            catch (IndexOutOfRangeException) { return null; }

            return record.IsDBNull(ordinal) ? null : ConvertValue(record.GetValue(ordinal), map.Key.PropertyType);
        }

        public static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null || value is DBNull) return null;

            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlying == typeof(Guid))
                return value is Guid g ? g : Guid.Parse(value.ToString()!);

            if (underlying == typeof(bool))
                return value is bool b ? b : Convert.ToInt32(value) != 0;

            if (underlying == typeof(DateTime))
                return value is DateTime dt ? dt : DateTime.Parse(value.ToString()!);

            if (underlying.IsEnum)
                return Enum.ToObject(underlying, Convert.ToInt32(value));

            if (underlying == typeof(decimal))
                return Convert.ToDecimal(value);

            return Convert.ChangeType(value, underlying);
        }
    }
}
