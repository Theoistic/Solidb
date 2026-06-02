using System;
using System.Collections.Generic;
using System.Text;
using Solidb.Mapping;
using Solidb.Providers;

namespace Solidb.Schema
{
    public sealed class SchemaBuilder
    {
        private readonly SqlDialect _dialect;

        public SchemaBuilder(SqlDialect dialect) => _dialect = dialect;

        public string BuildCreateTable(EntityMap map)
        {
            var sb = new StringBuilder();
            sb.Append($"CREATE TABLE IF NOT EXISTS {map.TableName} (");

            var cols = new List<string>();
            foreach (var prop in map.Properties)
            {
                var colType = GetColumnType(prop.PropertyType, prop.IsKey);
                var constraints = BuildConstraints(prop);
                cols.Add($"{prop.ColumnName} {colType}{constraints}");
            }

            sb.Append(string.Join(", ", cols));
            sb.Append(')');
            return sb.ToString();
        }

        public string BuildDropTable(EntityMap map) =>
            $"DROP TABLE IF EXISTS {map.TableName}";

        public string BuildAddColumn(EntityMap map, PropertyMap column)
        {
            var colType = GetColumnType(column.PropertyType, column.IsKey);
            return $"ALTER TABLE {map.TableName} ADD COLUMN {column.ColumnName} {colType}";
        }

        private string GetColumnType(Type propertyType, bool isKey)
        {
            var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (_dialect == SqlDialect.SQLite)
            {
                if (underlying == typeof(int) || underlying == typeof(long)
                    || underlying == typeof(short) || underlying == typeof(bool)
                    || underlying.IsEnum)
                    return "INTEGER";

                if (underlying == typeof(double) || underlying == typeof(float)
                    || underlying == typeof(decimal))
                    return "REAL";

                if (underlying == typeof(byte[]))
                    return "BLOB";

                // string, Guid, DateTime, etc. → TEXT
                return "TEXT";
            }
            else // SqlServer
            {
                if (underlying == typeof(int)) return "INT";
                if (underlying == typeof(long)) return "BIGINT";
                if (underlying == typeof(short)) return "SMALLINT";
                if (underlying == typeof(bool)) return "BIT";
                if (underlying == typeof(double) || underlying == typeof(float)) return "FLOAT";
                if (underlying == typeof(decimal)) return "DECIMAL(18,2)";
                if (underlying == typeof(DateTime)) return "DATETIME2";
                if (underlying == typeof(Guid)) return "UNIQUEIDENTIFIER";
                if (underlying == typeof(byte[])) return "VARBINARY(MAX)";
                if (underlying == typeof(string)) return "NVARCHAR(MAX)";
                if (underlying.IsEnum) return "INT";
                return "NVARCHAR(MAX)";
            }
        }

        private static string BuildConstraints(PropertyMap prop)
        {
            var constraints = new List<string>();

            if (prop.IsKey)
                constraints.Add("PRIMARY KEY");

            if (!prop.IsKey && IsNonNullable(prop.PropertyType))
                constraints.Add("NOT NULL");

            return constraints.Count > 0 ? " " + string.Join(" ", constraints) : string.Empty;
        }

        private static bool IsNonNullable(Type type)
        {
            if (type == typeof(string)) return false; // strings are nullable by default in DB
            return Nullable.GetUnderlyingType(type) == null && !type.IsClass;
        }
    }
}
