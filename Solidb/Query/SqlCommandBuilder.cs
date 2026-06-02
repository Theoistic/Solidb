using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Solidb.Mapping;

namespace Solidb.Query
{
    public sealed class SqlCommandBuilder
    {
        private readonly EntityMap _map;
        private readonly QueryState _state;

        public SqlCommandBuilder(EntityMap map, QueryState state)
        {
            _map = map;
            _state = state;
        }

        public string BuildSelect(string? tableAlias = null)
        {
            var alias = tableAlias ?? _map.TableName;
            var sb = new StringBuilder("SELECT ");

            if (tableAlias != null)
                sb.Append(string.Join(", ", _map.Properties.Select(p => $"{alias}.{p.ColumnName}")));
            else
                sb.Append(string.Join(", ", _map.Properties.Select(p => p.ColumnName)));

            sb.Append($" FROM {_map.TableName}");
            if (tableAlias != null) sb.Append($" {tableAlias}");

            AppendWhere(sb, tableAlias);
            AppendOrderBy(sb, tableAlias);
            AppendPaging(sb);
            return sb.ToString();
        }

        public string BuildSelectWithJoin(EntityMap joinMap, RelationMap relation, string tableAlias, string joinAlias)
        {
            var sb = new StringBuilder("SELECT ");
            var mainCols = _map.Properties.Select(p => $"{tableAlias}.{p.ColumnName} AS {tableAlias}_{p.ColumnName}");
            var joinCols = joinMap.Properties.Select(p => $"{joinAlias}.{p.ColumnName} AS {joinAlias}_{p.ColumnName}");
            sb.Append(string.Join(", ", mainCols.Concat(joinCols)));
            sb.Append($" FROM {_map.TableName} {tableAlias}");
            sb.Append($" LEFT JOIN {joinMap.TableName} {joinAlias}");
            sb.Append($" ON {tableAlias}.{relation.ForeignKeyName} = {joinAlias}.{joinMap.Key.ColumnName}");
            AppendWhere(sb, tableAlias);
            AppendOrderBy(sb, tableAlias);
            AppendPaging(sb);
            return sb.ToString();
        }

        /// <summary>Builds a SELECT...WHERE pk IN (...) using inline string ids (for split-query includes).</summary>
        public string BuildSelectIn(string columnName, IEnumerable<string> paramNames)
        {
            var sb = new StringBuilder("SELECT ");
            sb.Append(string.Join(", ", _map.Properties.Select(p => p.ColumnName)));
            sb.Append($" FROM {_map.TableName}");
            sb.Append($" WHERE {columnName} IN (");
            sb.Append(string.Join(", ", paramNames.Select(n => $"@{n}")));
            sb.Append(')');
            return sb.ToString();
        }

        public string BuildCount()
        {
            var sb = new StringBuilder($"SELECT COUNT(*) FROM {_map.TableName}");
            AppendWhere(sb);
            return sb.ToString();
        }

        public string BuildInsert()
        {
            var props = _map.Properties.Where(p => !p.IsGenerated).ToList();
            var columns = string.Join(", ", props.Select(p => p.ColumnName));
            var parameters = string.Join(", ", props.Select(p => $"@{p.PropertyName}"));
            return $"INSERT INTO {_map.TableName} ({columns}) VALUES ({parameters})";
        }

        public string BuildUpdate(IEnumerable<string> changedPropertyNames)
        {
            var names = changedPropertyNames.ToList();
            if (names.Count == 0) return string.Empty;
            var sets = string.Join(", ", names.Select(n =>
            {
                var prop = _map.Properties.First(p => p.PropertyName == n);
                return $"{prop.ColumnName} = @{prop.PropertyName}";
            }));
            return $"UPDATE {_map.TableName} SET {sets} WHERE {_map.Key.ColumnName} = @{_map.Key.PropertyName}";
        }

        public string BuildDelete() =>
            $"DELETE FROM {_map.TableName} WHERE {_map.Key.ColumnName} = @{_map.Key.PropertyName}";

        private void AppendWhere(StringBuilder sb, string? tableAlias = null)
        {
            if (_state.WhereExpressions.Count == 0) return;

            var translator = new SqlExpressionTranslator(_map, _state);
            var conditions = _state.WhereExpressions
                .Select(e =>
                {
                    var sql = translator.Translate(e);
                    return tableAlias != null ? PrefixColumns(sql, _map, tableAlias) : sql;
                })
                .ToList();

            sb.Append(" WHERE ");
            sb.Append(string.Join(" AND ", conditions));
        }

        private void AppendOrderBy(StringBuilder sb, string? tableAlias = null)
        {
            if (_state.OrderByExpressions.Count == 0) return;

            var parts = _state.OrderByExpressions.Select(o =>
            {
                var colName = o.Expression.Body is MemberExpression member
                    ? _map.Properties.FirstOrDefault(p => p.PropertyName == member.Member.Name)?.ColumnName ?? member.Member.Name
                    : o.Expression.Body.ToString();

                return tableAlias != null
                    ? $"{tableAlias}.{colName} {(o.Ascending ? "ASC" : "DESC")}"
                    : $"{colName} {(o.Ascending ? "ASC" : "DESC")}";
            });

            sb.Append(" ORDER BY ").Append(string.Join(", ", parts));
        }

        private void AppendPaging(StringBuilder sb)
        {
            if (_state.Take.HasValue) sb.Append($" LIMIT {_state.Take.Value}");
            if (_state.Skip.HasValue) sb.Append($" OFFSET {_state.Skip.Value}");
        }

        private static string PrefixColumns(string sql, EntityMap map, string alias)
        {
            // Prefix bare column names with alias (simple replacement)
            foreach (var prop in map.Properties)
                sql = sql.Replace($" {prop.ColumnName} ", $" {alias}.{prop.ColumnName} ")
                         .Replace($"({prop.ColumnName} ", $"({alias}.{prop.ColumnName} ")
                         .Replace($" {prop.ColumnName})", $" {alias}.{prop.ColumnName})");
            return sql;
        }
    }
}
